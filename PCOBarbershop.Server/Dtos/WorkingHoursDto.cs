namespace PCOBarbershop.Server.Dtos
{
   public record WorkingHoursDto(DayOfWeek DayOfWeek, TimeOnly StartTime, TimeOnly EndTime);
}
