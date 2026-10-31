namespace PCOBarbershop.Server.Settings
{
    public class StripeSettings
    {
        public const string SectionName = "Stripe";

        // Cheia secreta de test (sk_test_...) si secretul pentru webhook (whsec_...), din User Secrets
        public string SecretKey { get; set; } = string.Empty;
        public string WebhookSecret { get; set; } = string.Empty;

        public string Currency { get; set; } = "ron";

        // Adresa aplicatiei Angular, unde Stripe trimite clientul dupa plata
        public string ClientUrl { get; set; } = "https://localhost:49297";

        // Cat timp tine ocupat slotul o programare neplatita
        public int PaymentTimeoutMinutes { get; set; } = 30;
    }
}
