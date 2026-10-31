using Microsoft.EntityFrameworkCore;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Models;

namespace PCOBarbershop.Server.Services;

// Ruleaza in fundal o data pe minut si anuleaza programarile neplatite la timp,
// ca sloturile ocupate de ele sa redevina libere pentru alti clienti.
public class UnpaidAppointmentsCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UnpaidAppointmentsCleanupService> _logger;

    public UnpaidAppointmentsCleanupService(
        IServiceScopeFactory scopeFactory,
        ILogger<UnpaidAppointmentsCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var now = DateTime.UtcNow;

                var cancelled = await db.Appointments
                    .Where(a => a.Status == AppointmentStatus.PendingPayment
                                && a.PaymentExpiresAt != null
                                && a.PaymentExpiresAt < now)
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, AppointmentStatus.Cancelled), stoppingToken);

                if (cancelled > 0)
                {
                    _logger.LogInformation("{Count} programari neplatite au expirat si au fost anulate", cancelled);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Eroare la anularea programarilor neplatite");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
