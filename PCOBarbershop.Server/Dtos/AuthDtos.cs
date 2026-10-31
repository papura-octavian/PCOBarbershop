using System.ComponentModel.DataAnnotations;

namespace PCOBarbershop.Server.Dtos;

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string LastName { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class ConfirmEmailRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(10)]
    public string Code { get; set; } = string.Empty;
}

public class ResendConfirmationRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class TwoFactorCodeRequest
{
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
}

public class LoginTwoFactorRequest
{
    [Required]
    public string TwoFactorToken { get; set; } = string.Empty;

    // Codul de 6 cifre din Google Authenticator sau un cod de recuperare
    [Required, StringLength(20)]
    public string Code { get; set; } = string.Empty;
}

public class GoogleLoginRequest
{
    // "credential" primit de la butonul Google Sign-In (un ID token JWT semnat de Google)
    [Required]
    public string IdToken { get; set; } = string.Empty;
}

public record GoogleConfigDto(bool Enabled, string ClientId);

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string Email, IList<string> Roles);

// Raspunsul la login cand contul are 2FA: mai trebuie trimis codul din aplicatie
public record TwoFactorRequiredResponse(bool RequiresTwoFactor, string TwoFactorToken);

public record TwoFactorStatusDto(bool Enabled, int RecoveryCodesLeft);

public record TwoFactorSetupDto(string SharedKey, string QrCodeDataUrl);

public record RecoveryCodesDto(List<string> RecoveryCodes);