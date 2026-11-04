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
[Route("api/[controller]")]   // => /api/appointments
public class AppointmentsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IBookingService _booking;

    public AppointmentsController(AppDbContext db, IBookingService booking)
    {
        _db = db;
        _booking = booking;
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

        // Regulile rezervarii stau in BookingService (le foloseste si asistentul AI)
        var result = await _booking.CreateAsync(clientId, request.BarberId, request.ServiceId, request.StartUtc);

        switch (result.Outcome)
        {
            case BookingOutcome.ServiceNotFound:
                return NotFound(new { message = "Serviciul nu exista." });
            case BookingOutcome.BarberNotFound:
                return NotFound(new { message = "Frizerul nu exista." });
            case BookingOutcome.SlotNotFree:
                return Conflict(new { message = "Ora aleasa nu mai este disponibila." });
            case BookingOutcome.SlotJustTaken:
                return Conflict(new { message = "Ora aleasa tocmai a fost rezervata de altcineva. Alege alta ora." });
        }

        var appointment = result.Appointment!;

        var dto = new AppointmentDto(
            appointment.Id,
            result.Barber!.DisplayName,
            result.Service!.Name,
            result.Service.Price,
            appointment.StartTime,
            appointment.EndTime,
            appointment.Status.ToString(),
            appointment.PaymentExpiresAt);

        return Created($"/api/appointments/{appointment.Id}", dto);
    }

    // GET /api/appointments/mine - programarile clientului logat, cele mai noi primele
    [Authorize(Roles = "Client")]
    [HttpGet("mine")]
    public async Task<ActionResult<List<AppointmentDto>>> GetMine()
    {
        var clientId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        var appointments = await _db.Appointments
            .AsNoTracking()
            .Where(a => a.ClientId == clientId)
            .OrderByDescending(a => a.StartTime)
            .Select(a => new AppointmentDto(
                a.Id,
                a.Barber.DisplayName,
                a.Service.Name,
                a.Service.Price,
                a.StartTime,
                a.EndTime,
                a.Status.ToString(),
                a.Status == AppointmentStatus.PendingPayment ? a.PaymentExpiresAt : null))
            .ToListAsync();

        return Ok(appointments);
    }
}