namespace PCOBarbershop.Server.Models;

public class Appointment
{
    public int Id { get; set; }

    public string ClientId { get; set; } = string.Empty;
    public ApplicationUser Client { get; set; } = null!;

    public int BarberId { get; set; }
    public Barber Barber { get; set; } = null!;

    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;

    // Salvate in UTC
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }

    public AppointmentStatus Status { get; set; } = AppointmentStatus.PendingPayment;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}