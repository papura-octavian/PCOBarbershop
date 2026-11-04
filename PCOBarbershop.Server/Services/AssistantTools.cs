using System.ComponentModel;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Settings;

namespace PCOBarbershop.Server.Services;

// Functions the AI model is allowed to call.
// Registered as scoped (one instance per HTTP request), so it can safely use AppDbContext
// and remember things about the current request (who the client is, what was booked).
public class AssistantTools
{
    private readonly AppDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly IBookingService _booking;
    private readonly StripeSettings _stripeSettings;
    private readonly ILogger<AssistantTools> _logger;

    public AssistantTools(
        AppDbContext db,
        IAvailabilityService availability,
        IBookingService booking,
        IOptions<StripeSettings> stripeOptions,
        ILogger<AssistantTools> logger)
    {
        _db = db;
        _availability = availability;
        _booking = booking;
        _stripeSettings = stripeOptions.Value;
        _logger = logger;
    }

    // Set by AssistantService from the JWT token. The model never chooses who the booking is for.
    public string? ClientId { get; set; }

    // Set when create_booking succeeds; sent back to Angular so it can open the payment page
    public int? CreatedAppointmentId { get; private set; }

    // The list sent to the model. Each tool description comes from the [Description] on its method.
    public IList<AITool> GetTools() =>
    [
        AIFunctionFactory.Create(GetFreeSlotsAsync, "get_free_slots"),
        AIFunctionFactory.Create(CreateBookingAsync, "create_booking")
    ];

    [Description("Returns the free appointment start times for a barber, a service and a day. Use it whenever the user asks about availability or wants to book.")]
    public async Task<string> GetFreeSlotsAsync(
        [Description("The barber's name, as listed in the system prompt.")] string barberName,
        [Description("The service name, as listed in the system prompt.")] string serviceName,
        [Description("The day to check, in yyyy-MM-dd format, for example \"2026-11-05\".")] string date)
    {
        _logger.LogInformation("Tool get_free_slots: barber={Barber}, service={Service}, date={Date}",
            barberName, serviceName, date);

        // Bad input never throws: we return a clear message so the model can correct itself or ask the user
        if (!TryParseDate(date, out var day))
        {
            return $"Invalid date \"{date}\". Use the yyyy-MM-dd format.";
        }

        var (barber, barberError) = await FindBarberAsync(barberName);
        if (barber is null)
        {
            return barberError!;
        }

        var (service, serviceError) = await FindServiceAsync(serviceName);
        if (service is null)
        {
            return serviceError!;
        }

        // Same logic the booking page uses
        var slots = await _availability.GetFreeSlotsAsync(barber.Id, service.DurationMinutes, day);
        var dayText = FormatDate(day);

        if (slots.Count == 0)
        {
            return $"{barber.DisplayName} has no free slots on {dayText} ({day.DayOfWeek}) for {service.Name}. " +
                   "It can be a day off, a past day, too far ahead, or fully booked.";
        }

        return $"{barber.DisplayName} is free on {dayText} ({day.DayOfWeek}) for {service.Name} " +
               $"({service.DurationMinutes} min) at: {string.Join(", ", slots.Select(s => s.Time))}.";
    }

    [Description("Books an appointment for the logged-in user and starts the card payment. " +
                 "Call it ONLY after you summarized the booking and the user explicitly confirmed it in their last message.")]
    public async Task<string> CreateBookingAsync(
        [Description("The barber's name, as listed in the system prompt.")] string barberName,
        [Description("The service name, as listed in the system prompt.")] string serviceName,
        [Description("The day, in yyyy-MM-dd format, for example \"2026-11-05\".")] string date,
        [Description("The start time in 24h HH:mm format, exactly one of the times returned by get_free_slots, for example \"15:00\".")] string time)
    {
        _logger.LogInformation("Tool create_booking: barber={Barber}, service={Service}, date={Date}, time={Time}",
            barberName, serviceName, date, time);

        if (ClientId is null)
        {
            return "Booking is not available right now. Tell the user to use the booking page.";
        }

        // One booking per message is enough: protects against the model calling the tool twice
        if (CreatedAppointmentId is not null)
        {
            return "A booking was already created for this message. Do not create another one.";
        }

        if (!TryParseDate(date, out var day))
        {
            return $"Invalid date \"{date}\". Use the yyyy-MM-dd format.";
        }

        if (!TimeOnly.TryParseExact(time, ["HH:mm", "H:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
        {
            return $"Invalid time \"{time}\". Use the HH:mm format, for example 15:00.";
        }

        var (barber, barberError) = await FindBarberAsync(barberName);
        if (barber is null)
        {
            return barberError!;
        }

        var (service, serviceError) = await FindServiceAsync(serviceName);
        if (service is null)
        {
            return serviceError!;
        }

        // The model sends the salon's local time; the database works in UTC
        var startUtc = SalonTime.ToUtc(day, startTime);
        var timeText = startTime.ToString("HH:mm", CultureInfo.InvariantCulture);
        var dayText = FormatDate(day);

        // The same rules as the booking page: the slot is checked again here,
        // we never trust that the model checked it before
        var result = await _booking.CreateAsync(ClientId, barber.Id, service.Id, startUtc);

        if (result.Outcome is BookingOutcome.SlotNotFree or BookingOutcome.SlotJustTaken)
        {
            return $"{barber.DisplayName} is not free on {dayText} at {timeText} for {service.Name}. " +
                   "Nothing was booked. Call get_free_slots and offer the user other times.";
        }

        if (result.Outcome != BookingOutcome.Created)
        {
            return "The booking could not be created. Nothing was booked.";
        }

        CreatedAppointmentId = result.Appointment!.Id;

        return $"Booking created: {service.Name} with {barber.DisplayName} on {dayText} ({day.DayOfWeek}) at {timeText}, " +
               $"price {service.Price.ToString(CultureInfo.InvariantCulture)} RON. " +
               $"It is held for {_stripeSettings.PaymentTimeoutMinutes} minutes and is cancelled if it is not paid. " +
               "The app is now opening the card payment page automatically. Tell the user this in one or two short sentences.";
    }

    private static bool TryParseDate(string date, out DateOnly day) =>
        DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day);

    private static string FormatDate(DateOnly day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<(Barber? Barber, string? Error)> FindBarberAsync(string barberName)
    {
        var barbers = await _db.Barbers.AsNoTracking().ToListAsync();
        var barber = FindByName(barbers, b => b.DisplayName, barberName);

        return barber is null
            ? (null, $"There is no barber named \"{barberName}\". Barbers: {string.Join(", ", barbers.Select(b => b.DisplayName))}.")
            : (barber, null);
    }

    private async Task<(Service? Service, string? Error)> FindServiceAsync(string serviceName)
    {
        var services = await _db.Services.AsNoTracking().ToListAsync();
        var service = FindByName(services, s => s.Name, serviceName);

        return service is null
            ? (null, $"There is no service named \"{serviceName}\". Services: {string.Join(", ", services.Select(s => s.Name))}.")
            : (service, null);
    }

    // Exact match first (ignoring case), then partial match ("Andrei" finds "Andrei Popescu")
    private static T? FindByName<T>(List<T> items, Func<T, string> getName, string name) where T : class
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var trimmed = name.Trim();
        return items.FirstOrDefault(i => getName(i).Equals(trimmed, StringComparison.OrdinalIgnoreCase))
            ?? items.FirstOrDefault(i => getName(i).Contains(trimmed, StringComparison.OrdinalIgnoreCase));
    }
}
