namespace PCOBarbershop.Server.Settings
{
    public class GoogleSettings
    {
        public const string SectionName = "Google";

        // OAuth Client ID din Google Cloud Console (tip "Web application").
        // Daca e gol, butonul "Continua cu Google" nu apare.
        public string ClientId { get; set; } = string.Empty;
    }
}
