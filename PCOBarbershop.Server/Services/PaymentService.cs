using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Settings;
using Stripe;
using Stripe.Checkout;

namespace PCOBarbershop.Server.Services;

public interface IPaymentService
{
    bool IsConfigured { get; }

    // Intoarce adresa paginii de plata Stripe Checkout pentru o programare neplatita
    Task<string> CreateCheckoutUrlAsync(Appointment appointment);

    // Verifica la Stripe sesiunea si confirma programarea daca e platita (idempotent)
    Task<Appointment?> ConfirmFromSessionAsync(string sessionId);

    // Proceseaza un eveniment trimis de Stripe pe webhook (semnatura e verificata)
    Task HandleWebhookAsync(string json, string signatureHeader);

    // Returneaza banii pentru o programare platita; true daca s-a facut refund
    Task<bool> RefundAsync(Appointment appointment);
}

public class PaymentService : IPaymentService
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly StripeSettings _settings;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(
        AppDbContext db,
        IEmailService emailService,
        IOptions<StripeSettings> options,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _emailService = emailService;
        _settings = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.SecretKey);

    private StripeClient Client => new(_settings.SecretKey);

    public async Task<string> CreateCheckoutUrlAsync(Appointment appointment)
    {
        var sessions = new SessionService(Client);

        // Daca exista deja o sesiune deschisa pentru programare, o refolosim:
        // asa clientul nu poate plati de doua ori aceeasi programare.
        if (appointment.StripeSessionId is not null)
        {
            var existing = await sessions.GetAsync(appointment.StripeSessionId);
            if (existing.Status == "open" && existing.Url is not null)
            {
                return existing.Url;
            }
        }

        var local = SalonTime.ToLocal(appointment.StartTime);

        // Stripe cere ca sesiunea sa expire peste minim 30 de minute
        var expiresAt = DateTime.UtcNow.AddMinutes(Math.Max(30, _settings.PaymentTimeoutMinutes));

        var options = new SessionCreateOptions
        {
            Mode = "payment",
            CustomerEmail = appointment.Client.Email,
            ClientReferenceId = appointment.Id.ToString(),
            Metadata = new Dictionary<string, string> { ["appointmentId"] = appointment.Id.ToString() },
            ExpiresAt = expiresAt,
            Locale = "ro",
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Quantity = 1,
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        Currency = _settings.Currency,
                        UnitAmount = (long)Math.Round(appointment.Service.Price * 100),   // in bani
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = appointment.Service.Name,
                            Description = $"Cu {appointment.Barber.DisplayName}, {local:dd.MM.yyyy} la {local:HH:mm}"
                        }
                    }
                }
            ],
            SuccessUrl = $"{_settings.ClientUrl.TrimEnd('/')}/payment/success?session_id={{CHECKOUT_SESSION_ID}}",
            CancelUrl = $"{_settings.ClientUrl.TrimEnd('/')}/my-appointments?payment=cancelled"
        };

        var session = await sessions.CreateAsync(options);

        appointment.StripeSessionId = session.Id;
        appointment.PaymentExpiresAt = expiresAt;
        await _db.SaveChangesAsync();

        return session.Url;
    }

    public async Task<Appointment?> ConfirmFromSessionAsync(string sessionId)
    {
        var session = await new SessionService(Client).GetAsync(sessionId);
        return await ApplyCompletedSessionAsync(session);
    }

    public async Task HandleWebhookAsync(string json, string signatureHeader)
    {
        // Arunca StripeException daca semnatura nu e valida (cererea nu vine de la Stripe)
        var stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _settings.WebhookSecret, throwOnApiVersionMismatch: false);

        if (stripeEvent.Data.Object is not Session session)
        {
            return;
        }

        switch (stripeEvent.Type)
        {
            case EventTypes.CheckoutSessionCompleted:
                await ApplyCompletedSessionAsync(session);
                break;

            case EventTypes.CheckoutSessionExpired:
                await CancelUnpaidAsync(session.Id);
                break;
        }
    }

    public async Task<bool> RefundAsync(Appointment appointment)
    {
        if (!IsConfigured || appointment.StripePaymentIntentId is null)
        {
            return false;
        }

        await new RefundService(Client).CreateAsync(new RefundCreateOptions
        {
            PaymentIntent = appointment.StripePaymentIntentId
        });

        return true;
    }

    private async Task<Appointment?> ApplyCompletedSessionAsync(Session session)
    {
        var appointment = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Include(a => a.Barber)
            .FirstOrDefaultAsync(a => a.StripeSessionId == session.Id);

        if (appointment is null)
        {
            _logger.LogWarning("Sesiune Stripe {SessionId} fara programare asociata", session.Id);
            return null;
        }

        if (session.PaymentStatus != "paid")
        {
            return appointment;
        }

        // Deja confirmata (de exemplu si webhook-ul si pagina de succes au ajuns aici): nu facem nimic
        if (appointment.Status == AppointmentStatus.Confirmed)
        {
            return appointment;
        }

        appointment.StripePaymentIntentId = session.PaymentIntentId;
        appointment.PaidAt = DateTime.UtcNow;

        // Plata a venit dupa ce programarea a expirat/a fost anulata: slotul poate fi ocupat, returnam banii
        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            await _db.SaveChangesAsync();
            await RefundAsync(appointment);
            _logger.LogWarning("Plata pentru programarea anulata {Id} a fost returnata", appointment.Id);
            return appointment;
        }

        appointment.Status = AppointmentStatus.Confirmed;
        await _db.SaveChangesAsync();

        await SendConfirmationEmailAsync(appointment);
        return appointment;
    }

    private async Task CancelUnpaidAsync(string sessionId)
    {
        var appointment = await _db.Appointments.FirstOrDefaultAsync(a => a.StripeSessionId == sessionId);
        if (appointment is { Status: AppointmentStatus.PendingPayment })
        {
            appointment.Status = AppointmentStatus.Cancelled;
            await _db.SaveChangesAsync();
        }
    }

    private async Task SendConfirmationEmailAsync(Appointment appointment)
    {
        var client = appointment.Client;
        var info = new AppointmentInfo(
            appointment.Service.Name,
            appointment.Barber.DisplayName,
            appointment.StartTime,
            appointment.Service.Price);

        try
        {
            await _emailService.SendAsync(
                client.Email!,
                $"{client.FirstName} {client.LastName}",
                "Programarea ta este confirmata",
                EmailTemplates.AppointmentConfirmed(client.FirstName, info));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nu s-a putut trimite confirmarea pentru programarea {Id}", appointment.Id);
        }
    }
}
