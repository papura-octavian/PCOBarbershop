using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Settings;

namespace PCOBarbershop.Server.Services;

public record TokenResult(string Token, DateTime ExpiresAtUtc);

public interface ITokenService
{
    TokenResult CreateToken(ApplicationUser user, IList<string> roles);

    // Token temporar (5 minute) dat dupa parola corecta, cand contul are 2FA activ.
    // Nu da acces la API: doar dovedeste ca primul pas al autentificarii a reusit.
    string CreateTwoFactorToken(ApplicationUser user);

    // Intoarce id-ul utilizatorului daca tokenul temporar e valid, altfel null
    Task<string?> ValidateTwoFactorTokenAsync(string token);
}

public class TokenService : ITokenService
{
    // Audienta diferita de cea a tokenului normal, ca JwtBearer sa nu il accepte pe rutele API
    private const string TwoFactorAudienceSuffix = ".2fa";
    private static readonly TimeSpan TwoFactorTokenLifetime = TimeSpan.FromMinutes(5);

    private readonly JwtSettings _settings;

    public TokenService(IOptions<JwtSettings> options)
    {
        _settings = options.Value;
    }

    private SymmetricSecurityKey SigningKey => new(Encoding.UTF8.GetBytes(_settings.Key));

    public TokenResult CreateToken(ApplicationUser user, IList<string> roles)
    {
        // Claims = informatiile din token. Oricine poate citi un JWT,
        // deci nu pui niciodata parole sau date sensibile in el.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var expires = DateTime.UtcNow.AddMinutes(_settings.ExpiresInMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience,
            Expires = expires,
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new TokenResult(token, expires);
    }

    public string CreateTwoFactorToken(ApplicationUser user)
    {
        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            }),
            Issuer = _settings.Issuer,
            Audience = _settings.Audience + TwoFactorAudienceSuffix,
            Expires = DateTime.UtcNow.Add(TwoFactorTokenLifetime),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public async Task<string?> ValidateTwoFactorTokenAsync(string token)
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = _settings.Issuer,
            ValidAudience = _settings.Audience + TwoFactorAudienceSuffix,
            IssuerSigningKey = SigningKey,
            ClockSkew = TimeSpan.Zero
        });

        return result.IsValid && result.Claims.TryGetValue(JwtRegisteredClaimNames.Sub, out var sub)
            ? sub as string
            : null;
    }
}
