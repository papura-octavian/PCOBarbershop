using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Services;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]   // => /api/barbers/...
public class BarbersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAvailabilityService _availability;
    private readonly IEmailService _emailService;
    private readonly IPaymentService _payments;
    private readonly ILogger<BarbersController> _logger;

    public BarbersController(
        AppDbContext db,
        IAvailabilityService availability,
        IEmailService emailService,
        IPaymentService payments,
        ILogger<BarbersController> logger)
    {
        _db = db;
        _availability = availability;
        _emailService = emailService;
        _payments = payments;
        _logger = logger;
    }

    // GET /api/barbers - lista frizerilor (public)
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<BarberDto>>> GetAll()
    {
        var barbers = await _db.Barbers
            .AsNoTracking()
            .OrderBy(b => b.DisplayName)
            .Select(b => new BarberDto(b.Id, b.DisplayName))
            .ToListAsync();

        return Ok(barbers);
    }

    // GET /api/barbers/1/slots?date=2026-10-26&serviceId=2 - orele libere (public)
    [AllowAnonymous]
    [HttpGet("{barberId:int}/slots")]
    public async Task<ActionResult<List<SlotDto>>> GetSlots(
        int barberId,
        [FromQuery] DateOnly? date,
        [FromQuery] int serviceId)
    {
        if (date is null)
        {
            return BadRequest(new { message = "Parametrul 'date' este obligatoriu (format yyyy-MM-dd)." });
        }

        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId);
        if (service is null)
        {
            return NotFound(new { message = "Serviciul nu exista." });
        }

        if (!await _db.Barbers.AnyAsync(b => b.Id == barberId))
        {
            return NotFound(new { message = "Frizerul nu exista." });
        }

        var slots = await _availability.GetFreeSlotsAsync(barberId, service.DurationMinutes, date.Value);
        return Ok(slots);
    }

    // Programul frizerului logat.
    // Identitatea vine din TOKEN, nu din URL si nu din corpul cererii. Astfel un frizer
    // nu poate cere programul altuia schimband un id.
    [Authorize(Roles = "Frizer")]
    [HttpGet("me/schedule")]
    public async Task<ActionResult<List<WorkingHoursDto>>> GetMySchedule()
    {
        var barberId = await GetCurrentBarberIdAsync();
        if (barberId is null)
        {
            return NotFound(new { message = "Nu exista profil de frizer pentru acest cont." });
        }

        var schedule = await _db.WorkingHours
            .AsNoTracking()
            .Where(w => w.BarberId == barberId)
            .OrderBy(w => ((int)w.DayOfWeek + 6) % 7)   // luni primul, duminica ultima
            .Select(w => new WorkingHoursDto(w.DayOfWeek, w.StartTime, w.EndTime))
            .ToListAsync();

        return Ok(schedule);
    }

    // PUT /api/barbers/me/schedule - frizerul isi inlocuieste programul saptamanal.
    // Zilele care lipsesc din lista sunt zile libere.
    [Authorize(Roles = "Frizer")]
    [HttpPut("me/schedule")]
    public async Task<ActionResult<List<WorkingHoursDto>>> UpdateMySchedule(List<WorkingHoursDto> items)
    {
        if (items.Select(i => i.DayOfWeek).Distinct().Count() != items.Count)
        {
            return BadRequest(new { message = "Fiecare zi poate aparea o singura data." });
        }

        if (items.Any(i => !Enum.IsDefined(i.DayOfWeek)))
        {
            return BadRequest(new { message = "Zi a saptamanii invalida." });
        }

        if (items.Any(i => i.StartTime >= i.EndTime))
        {
            return BadRequest(new { message = "Ora de inceput trebuie sa fie inaintea orei de sfarsit." });
        }

        var barberId = await GetCurrentBarberIdAsync();
        if (barberId is null)
        {
            return NotFound(new { message = "Nu exista profil de frizer pentru acest cont." });
        }

        // Stergem programul vechi si il salvam pe cel nou, intr-o singura tranzactie
        await using var transaction = await _db.Database.BeginTransactionAsync();

        await _db.WorkingHours.Where(w => w.BarberId == barberId).ExecuteDeleteAsync();

        _db.WorkingHours.AddRange(items.Select(i => new WorkingHours
        {
            BarberId = barberId.Value,
            DayOfWeek = i.DayOfWeek,
            StartTime = i.StartTime,
            EndTime = i.EndTime
        }));

        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        return await GetMySchedule();
    }

    // GET /api/barbers/me/appointments?from=2026-10-26&to=2026-11-01
    // Calendarul de rezervari al frizerului logat, intre doua zile (inclusiv), ora locala a salonului
    [Authorize(Roles = "Frizer")]
    [HttpGet("me/appointments")]
    public async Task<ActionResult<List<BarberAppointmentDto>>> GetMyAppointments(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to)
    {
        if (from is null || to is null || to < from)
        {
            return BadRequest(new { message = "Parametrii 'from' si 'to' sunt obligatorii (yyyy-MM-dd), iar 'to' >= 'from'." });
        }

        if (to.Value.DayNumber - from.Value.DayNumber > 62)
        {
            return BadRequest(new { message = "Intervalul maxim este de 62 de zile." });
        }

        var barberId = await GetCurrentBarberIdAsync();
        if (barberId is null)
        {
            return NotFound(new { message = "Nu exista profil de frizer pentru acest cont." });
        }

        var fromUtc = SalonTime.ToUtc(from.Value, TimeOnly.MinValue);
        var toUtc = SalonTime.ToUtc(to.Value.AddDays(1), TimeOnly.MinValue);

        var appointments = await _db.Appointments
            .AsNoTracking()
            .Where(a => a.BarberId == barberId && a.StartTime >= fromUtc && a.StartTime < toUtc)
            .OrderBy(a => a.StartTime)
            .Select(a => new BarberAppointmentDto(
                a.Id,
                a.Client.FirstName + " " + a.Client.LastName,
                a.Client.Email ?? string.Empty,
                a.Service.Name,
                a.Service.Price,
                a.StartTime,
                a.EndTime,
                a.Status.ToString()))
            .ToListAsync();

        return Ok(appointments);
    }

    // POST /api/barbers/me/appointments/5/cancel - frizerul anuleaza o programare viitoare.
    // Clientul este anuntat automat pe e-mail.
    [Authorize(Roles = "Frizer")]
    [HttpPost("me/appointments/{id:int}/cancel")]
    public async Task<ActionResult<BarberAppointmentDto>> CancelAppointment(int id, CancelAppointmentRequest request)
    {
        var barberId = await GetCurrentBarberIdAsync();
        if (barberId is null)
        {
            return NotFound(new { message = "Nu exista profil de frizer pentru acest cont." });
        }

        // Cautam doar printre programarile ACESTUI frizer: nu poate anula programarile altuia
        var appointment = await _db.Appointments
            .Include(a => a.Client)
            .Include(a => a.Service)
            .Include(a => a.Barber)
            .FirstOrDefaultAsync(a => a.Id == id && a.BarberId == barberId);

        if (appointment is null)
        {
            return NotFound(new { message = "Programarea nu exista." });
        }

        if (appointment.Status == AppointmentStatus.Cancelled)
        {
            return Conflict(new { message = "Programarea este deja anulata." });
        }

        if (appointment.StartTime <= DateTime.UtcNow)
        {
            return BadRequest(new { message = "Nu poti anula o programare care a inceput deja." });
        }

        appointment.Status = AppointmentStatus.Cancelled;
        await _db.SaveChangesAsync();

        // Daca programarea era platita, returnam banii clientului
        var refunded = false;
        if (appointment.StripePaymentIntentId is not null)
        {
            try
            {
                refunded = await _payments.RefundAsync(appointment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Refund esuat pentru programarea {Id}", appointment.Id);
            }
        }

        await NotifyClientAboutCancellationAsync(appointment, request.Reason, refunded);

        return Ok(new BarberAppointmentDto(
            appointment.Id,
            $"{appointment.Client.FirstName} {appointment.Client.LastName}",
            appointment.Client.Email ?? string.Empty,
            appointment.Service.Name,
            appointment.Service.Price,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Status.ToString()));
    }

    // Daca e-mailul nu pleaca, anularea ramane valabila; doar scriem eroarea in log
    private async Task NotifyClientAboutCancellationAsync(Appointment appointment, string? reason, bool refunded)
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
                "Programarea ta a fost anulata",
                EmailTemplates.AppointmentCancelled(client.FirstName, info, reason, refunded));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Nu s-a putut trimite e-mailul de anulare pentru programarea {Id}", appointment.Id);
        }
    }

    // Id-ul frizerului din tabela Barbers, pentru contul din token
    private async Task<int?> GetCurrentBarberIdAsync()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        return await _db.Barbers
            .Where(b => b.UserId == userId)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();
    }
}