namespace PCOBarbershop.Server.Models;

public class Barber
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;

    public string DisplayName { get; set; } = string.Empty;

    public List<WorkingHours> Schedule { get; set; } = new();
    public List<Appointment> Appointments { get; set; } = new();
}