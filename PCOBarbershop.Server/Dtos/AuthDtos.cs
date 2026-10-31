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

public record AuthResponse(string Token, DateTime ExpiresAtUtc, string Email, IList<string> Roles);