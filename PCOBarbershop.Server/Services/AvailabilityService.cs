using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Models;

namespace PCOBarbershop.Server.Services;

public interface IAvailabilityService
{
    Task<List<SlotDto>> GetFreeSlotsAsync(int barberId, int durationMinutes, DateOnly date);
    Task<bool> IsSlotFreeAsync(int barberId, int durationMinutes, DateTime startUtc);
}

public class AvailabilityService : IAvailabilityService
{
    // Un slot poate incepe din 20 in 20 de minute (cel mai mare divizor comun al duratelor: 20, 40, 60)
    private const int SlotStepMinutes = 20;

    // Cat de departe in viitor se poate rezerva
    private const int MaxDaysAhead = 60;

    private readonly AppDbContext _db;

    public AvailabilityService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<SlotDto>> GetFreeSlotsAsync(int barberId, int durationMinutes, DateOnly date)
    {
        var nowUtc = DateTime.UtcNow;
        var todayLocal = SalonTime.Today;

        // zile trecute sau prea departe in viitor: nu exista sloturi
        if (date < todayLocal || date > todayLocal.AddDays(MaxDaysAhead))
        {
            return new List<SlotDto>();
        }

        // 1. programul frizerului in ziua respectiva a saptamanii
        var hours = await _db.WorkingHours
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.BarberId == barberId && w.DayOfWeek == date.DayOfWeek);

        if (hours is null)
        {
            return new List<SlotDto>();   // zi libera
        }

        var windowStartUtc = SalonTime.ToUtc(date, hours.StartTime);
        var windowEndUtc = SalonTime.ToUtc(date, hours.EndTime);

        // 2. programarile existente care ating intervalul de lucru (fara cele anulate)
        var busy = await _db.Appointments
            .AsNoTracking()
            .Where(a => a.BarberId == barberId
                        && a.Status != AppointmentStatus.Cancelled
                        && a.StartTime < windowEndUtc
                        && a.EndTime > windowStartUtc)
            .Select(a => new { a.StartTime, a.EndTime })
            .ToListAsync();

        // 3. generam slotul si il pastram doar daca incape, e in viitor si e liber
        var duration = TimeSpan.FromMinutes(durationMinutes);
        var step = TimeSpan.FromMinutes(SlotStepMinutes);
        var slots = new List<SlotDto>();

        for (var start = windowStartUtc; start + duration <= windowEndUtc; start += step)
        {
            var end = start + duration;

            if (start <= nowUtc)
            {
                continue;
            }

            var overlaps = busy.Any(b => b.StartTime < end && b.EndTime > start);
            if (overlaps)
            {
                continue;
            }

            var local = SalonTime.ToLocal(start);
            slots.Add(new SlotDto(start, local.ToString("HH:mm", CultureInfo.InvariantCulture)));
        }

        return slots;
    }

    // Reverifica, la momentul rezervarii, ca ora ceruta e inca un slot liber valid
    // (in program, pe grila, in viitor, fara suprapuneri)
    public async Task<bool> IsSlotFreeAsync(int barberId, int durationMinutes, DateTime startUtc)
    {
        var localDate = DateOnly.FromDateTime(SalonTime.ToLocal(startUtc));
        var slots = await GetFreeSlotsAsync(barberId, durationMinutes, localDate);

        return slots.Any(s => s.StartUtc == startUtc);
    }
}