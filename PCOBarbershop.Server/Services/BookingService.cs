using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Settings;

namespace PCOBarbershop.Server.Services;

public enum BookingOutcome
{
    Created,
    ServiceNotFound,
    BarberNotFound,
    SlotNotFree,     // the code check said the slot is not free
    SlotJustTaken    // the database constraint caught a booking made at the same moment
}

// Appointment, Barber and Service are set only when Outcome is Created
public record BookingResult(BookingOutcome Outcome, Appointment? Appointment = null, Barber? Barber = null, Service? Service = null);

public interface IBookingService
{
    // Creates an unpaid appointment (PendingPayment) that holds the slot for a few minutes
    Task<BookingResult> CreateAsync(string clientId, int barberId, int serviceId, DateTime startUtc);
}

// Used by POST /api/appointments (booking page) and by the AI assistant (create_booking tool),
// so both follow exactly the same rules
public class BookingService : IBookingService
{
    private readonly AppDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly StripeSettings _stripeSettings;

    public BookingService(AppDbContext db, IAvailabilityService availability, IOptions<StripeSettings> stripeOptions)
    {
        _db = db;
        _availability = availability;
        _stripeSettings = stripeOptions.Value;
    }

    public async Task<BookingResult> CreateAsync(string clientId, int barberId, int serviceId, DateTime startUtc)
    {
        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId);
        if (service is null)
        {
            return new BookingResult(BookingOutcome.ServiceNotFound);
        }

        var barber = await _db.Barbers.AsNoTracking().FirstOrDefaultAsync(b => b.Id == barberId);
        if (barber is null)
        {
            return new BookingResult(BookingOutcome.BarberNotFound);
        }

        // Layer 1: the check in code
        if (!await _availability.IsSlotFreeAsync(barber.Id, service.DurationMinutes, startUtc))
        {
            return new BookingResult(BookingOutcome.SlotNotFree);
        }

        var appointment = new Appointment
        {
            ClientId = clientId,
            BarberId = barber.Id,
            ServiceId = service.Id,
            StartTime = startUtc,
            EndTime = startUtc.AddMinutes(service.DurationMinutes),
            Status = AppointmentStatus.PendingPayment,
            // The slot is held only for a few minutes; if it is not paid, it is released automatically
            PaymentExpiresAt = DateTime.UtcNow.AddMinutes(_stripeSettings.PaymentTimeoutMinutes)
        };

        _db.Appointments.Add(appointment);

        try
        {
            await _db.SaveChangesAsync();
        }
        // Layer 2: the database constraint rejected an overlap (PostgreSQL code 23P01)
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            _db.Entry(appointment).State = EntityState.Detached;
            return new BookingResult(BookingOutcome.SlotJustTaken);
        }

        return new BookingResult(BookingOutcome.Created, appointment, barber, service);
    }
}
