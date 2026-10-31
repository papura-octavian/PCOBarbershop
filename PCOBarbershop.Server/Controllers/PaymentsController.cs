using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Services;
using Stripe;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]   // => /api/payments
public class PaymentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPaymentService _payments;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(AppDbContext db, IPaymentService payments, ILogger<PaymentsController> logger)
    {
        _db = db;
        _payments = payments;
        _logger = logger;
    }

    // POST /api/payments/checkout/5 - porneste plata cu cardul pentru programarea 5.
    // Raspunsul contine adresa paginii de plata Stripe, unde Angular redirectioneaza clientul.
    [Authorize(Roles = "Client")]
    [HttpPost("checkout/{appointmentId:int}")]
    public async Task<ActionResult<CheckoutResponse>> Checkout(int appointmentId)
    {
        if (!_payments.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Plata online nu este configurata pe server." });
        }

        var clientId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        var appointment = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Include(a => a.Barber)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.ClientId == clientId);

        if (appointment is null)
        {
            return NotFound(new { message = "Programarea nu exista." });
        }

        if (appointment.Status != AppointmentStatus.PendingPayment)
        {
            return Conflict(new { message = "Programarea nu mai asteapta plata." });
        }

        if (appointment.PaymentExpiresAt < DateTime.UtcNow)
        {
            return Conflict(new { message = "Timpul pentru plata a expirat. Fa o noua rezervare." });
        }

        try
        {
            var url = await _payments.CreateCheckoutUrlAsync(appointment);
            return Ok(new CheckoutResponse(url));
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Eroare Stripe la crearea platii pentru programarea {Id}", appointmentId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Nu am putut porni plata. Incearca din nou." });
        }
    }

    // POST /api/payments/confirm - apelat de pagina de succes dupa intoarcerea de la Stripe.
    // Verifica direct la Stripe ca plata s-a facut (nu ne bazam pe ce trimite browserul).
    [Authorize(Roles = "Client")]
    [HttpPost("confirm")]
    public async Task<ActionResult<AppointmentDto>> Confirm(ConfirmPaymentRequest request)
    {
        var clientId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        var belongsToClient = await _db.Appointments
            .AnyAsync(a => a.StripeSessionId == request.SessionId && a.ClientId == clientId);

        if (!belongsToClient || !_payments.IsConfigured)
        {
            return NotFound(new { message = "Plata nu a fost gasita." });
        }

        Appointment? appointment;
        try
        {
            appointment = await _payments.ConfirmFromSessionAsync(request.SessionId);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Eroare Stripe la verificarea sesiunii {SessionId}", request.SessionId);
            return StatusCode(StatusCodes.Status502BadGateway,
                new { message = "Nu am putut verifica plata. Reincarca pagina." });
        }

        if (appointment is null)
        {
            return NotFound(new { message = "Plata nu a fost gasita." });
        }

        return Ok(new AppointmentDto(
            appointment.Id,
            appointment.Barber.DisplayName,
            appointment.Service.Name,
            appointment.Service.Price,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Status.ToString(),
            appointment.PaymentExpiresAt));
    }

    // POST /api/payments/webhook - Stripe anunta aici plata finalizata sau sesiunea expirata.
    // Nu are token: autenticitatea se verifica prin semnatura din header-ul Stripe-Signature.
    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(Request.Body).ReadToEndAsync();
        var signature = Request.Headers["Stripe-Signature"].ToString();

        try
        {
            await _payments.HandleWebhookAsync(json, signature);
            return Ok();
        }
        catch (StripeException ex)
        {
            _logger.LogWarning(ex, "Webhook Stripe respins");
            return BadRequest();
        }
    }
}
