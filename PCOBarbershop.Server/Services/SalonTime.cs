namespace PCOBarbershop.Server.Services;

// Conversii intre ora locala a salonului si UTC (in baza de date totul e salvat in UTC)
public static class SalonTime
{
    // Fusul orar al salonului. Daca da eroare pe Windows, foloseste "E. Europe Standard Time".
    public static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Bucharest");

    public static DateOnly Today => DateOnly.FromDateTime(ToLocal(DateTime.UtcNow));

    // Ora locala a salonului -> UTC
    public static DateTime ToUtc(DateOnly date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.ToDateTime(time), DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(local, Zone);
    }

    // UTC -> ora locala a salonului
    public static DateTime ToLocal(DateTime utc)
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    }
}
