using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using PCOBarbershop.Server.Settings;

namespace PCOBarbershop.Server.Services;

public record GoogleUser(string Subject, string Email, bool EmailVerified, string FirstName, string LastName);

public interface IGoogleTokenValidator
{
    bool IsConfigured { get; }
    string ClientId { get; }

    // Verifica ID token-ul primit de la Google Sign-In; null daca nu e valid
    Task<GoogleUser?> ValidateAsync(string idToken);
}

// Verifica semnatura, emitentul, audienta si expirarea ID token-ului (OpenID Connect).
// Cheile publice Google sunt descarcate si tinute in cache de ConfigurationManager.
public class GoogleTokenValidator : IGoogleTokenValidator
{
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> GoogleConfiguration = new(
        "https://accounts.google.com/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever());

    private readonly GoogleSettings _settings;
    private readonly ILogger<GoogleTokenValidator> _logger;

    public GoogleTokenValidator(IOptions<GoogleSettings> options, ILogger<GoogleTokenValidator> logger)
    {
        _settings = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_settings.ClientId);

    public string ClientId => _settings.ClientId;

    public async Task<GoogleUser?> ValidateAsync(string idToken)
    {
        var configuration = await GoogleConfiguration.GetConfigurationAsync(CancellationToken.None);

        var result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidAudience = _settings.ClientId,   // tokenul trebuie sa fie emis pentru aplicatia NOASTRA
            IssuerSigningKeys = configuration.SigningKeys
        });

        if (!result.IsValid)
        {
            _logger.LogWarning(result.Exception, "ID token Google invalid");
            return null;
        }

        string? Claim(string name) => result.Claims.TryGetValue(name, out var value) ? value?.ToString() : null;

        var email = Claim("email");
        var subject = Claim("sub");

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(subject))
        {
            return null;
        }

        // email_verified poate veni ca bool sau ca text "true"
        var verified = string.Equals(Claim("email_verified"), "true", StringComparison.OrdinalIgnoreCase);

        return new GoogleUser(
            subject,
            email,
            verified,
            Claim("given_name") ?? email.Split('@')[0],
            Claim("family_name") ?? string.Empty);
    }
}
