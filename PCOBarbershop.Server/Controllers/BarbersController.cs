using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Services;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]   // => /api/barbers/...
public class BarbersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAvailabilityService _availability;

    public BarbersController(AppDbContext db, IAvailabilityService availability)
    {
        _db = db;
        _availability = availability;
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
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        var barberId = await _db.Barbers
            .Where(b => b.UserId == userId)
            .Select(b => (int?)b.Id)
            .FirstOrDefaultAsync();

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
}