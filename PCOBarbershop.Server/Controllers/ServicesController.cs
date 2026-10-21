using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]   // => /api/services
public class ServicesController : ControllerBase
{
    private readonly AppDbContext _db;

    public ServicesController(AppDbContext db)
    {
        _db = db;
    }

    // Public: oricine poate vedea serviciile si preturile, chiar fara cont
    [AllowAnonymous]
    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetAll()
    {
        var services = await _db.Services
            .AsNoTracking()   // citim doar, deci EF nu trebuie sa urmareasca modificari
            .OrderBy(s => s.Price)
            .Select(s => new ServiceDto(s.Id, s.Name, s.Price, s.DurationMinutes))
            .ToListAsync();

        return Ok(services);
    }
}