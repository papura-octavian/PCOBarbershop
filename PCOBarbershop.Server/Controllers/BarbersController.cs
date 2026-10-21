using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]   // => /api/barbers/...
public class BarbersController : ControllerBase
{
    private readonly AppDbContext _db;

    public BarbersController(AppDbContext db)
    {
        _db = db;
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