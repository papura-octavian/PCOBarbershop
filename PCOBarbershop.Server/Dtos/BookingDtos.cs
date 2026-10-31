namespace PCOBarbershop.Server.Dtos
{
    public record BarberDto(int Id, string DisplayName);

    // StartUtc se trimite inapoi la rezervare (#11); Time e ora locala afisata clientului
    public record SlotDto(DateTime StartUtc, string Time);

    public record CreateAppointmentRequest(int BarberId, int ServiceId, DateTime StartUtc);

    public record AppointmentDto(
        int Id,
        string BarberName,
        string ServiceName,
        decimal Price,
        DateTime StartUtc,
        DateTime EndUtc,
        string Status);

    public class CancelAppointmentRequest
    {
        [System.ComponentModel.DataAnnotations.StringLength(500)]
        public string? Reason { get; set; }
    }

    // Programare vazuta de frizer: include si datele clientului
    public record BarberAppointmentDto(
        int Id,
        string ClientName,
        string ClientEmail,
        string ServiceName,
        decimal Price,
        DateTime StartUtc,
        DateTime EndUtc,
        string Status);
}
