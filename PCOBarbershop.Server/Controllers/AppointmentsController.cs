using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Npgsql;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Services;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]   // => /api/appointments
public class AppointmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAvailabilityService _availability;

    public AppointmentsController(AppDbContext db, IAvailabilityService availability)
    {
        _db = db;
        _availability = availability;
    }

    // POST /api/appointments - doar clientii pot face rezervari
    [Authorize(Roles = "Client")]
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentRequest request)
    {
        // Ora trebuie trimisa in UTC (cu "Z" la final); altfel nu stim in ce fus e
        if (request.StartUtc.Kind != DateTimeKind.Utc)
        {
            return BadRequest(new { message = "startUtc trebuie sa fie in UTC (de forma 2026-10-26T07:00:00Z)." });
        }

        // Clientul vine din TOKEN, nu din cerere: nimeni nu poate rezerva in numele altcuiva
        var clientId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (clientId is null)
        {
            return Unauthorized();
        }

        var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceId);
        if (service is null)
        {
            return NotFound(new { message = "Serviciul nu exista." });
        }

        var barber = await _db.Barbers.AsNoTracking().FirstOrDefaultAsync(b => b.Id == request.BarberId);
        if (barber is null)
        {
            return NotFound(new { message = "Frizerul nu exista." });
        }

        // Strat 1: verificarea din cod
        if (!await _availability.IsSlotFreeAsync(barber.Id, service.DurationMinutes, request.StartUtc))
        {
            return Conflict(new { message = "Ora aleasa nu mai este disponibila." });
        }

        var appointment = new Appointment
        {
            ClientId = clientId,
            BarberId = barber.Id,
            ServiceId = service.Id,
            StartTime = request.StartUtc,
            EndTime = request.StartUtc.AddMinutes(service.DurationMinutes),
            Status = AppointmentStatus.PendingPayment
        };

        _db.Appointments.Add(appointment);

        try
        {
            await _db.SaveChangesAsync();
        }
        // Strat 2: constrangerea din baza de date a respins o suprapunere (cod PostgreSQL 23P01)
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.ExclusionViolation })
        {
            return Conflict(new { message = "Ora aleasa tocmai a fost rezervata de altcineva. Alege alta ora." });
        }

        var dto = new AppointmentDto(
            appointment.Id,
            barber.DisplayName,
            service.Name,
            service.Price,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Status.ToString());

        return Created($"/api/appointments/{appointment.Id}", dto);
    }
}