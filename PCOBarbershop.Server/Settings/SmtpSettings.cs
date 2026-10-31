namespace PCOBarbershop.Server.Settings
{
    public class SmtpSettings
    {
        public const string SectionName = "Smtp";

        // Daca Host e gol, e-mailurile nu se trimit, ci doar se scriu in log (util la dezvoltare)
        public string Host { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool UseStartTls { get; set; } = true;

        public string FromEmail { get; set; } = "no-reply@pcobarbershop.ro";
        public string FromName { get; set; } = "PCO Barbershop No-Reply";
    }
}
