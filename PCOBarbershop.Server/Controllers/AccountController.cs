using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Models;
using QRCoder;

namespace PCOBarbershop.Server.Controllers;

// Setarile de securitate ale contului logat: autentificarea in doi pasi (2FA / TOTP)
[ApiController]
[Authorize]
[Route("api/[controller]")]   // => /api/account
public class AccountController : ControllerBase
{
    private const string Issuer = "PCO Barbershop";
    private const int RecoveryCodesCount = 8;

    private readonly UserManager<ApplicationUser> _userManager;

    public AccountController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    // GET /api/account/2fa - starea 2FA pentru contul logat
    [HttpGet("2fa")]
    public async Task<ActionResult<TwoFactorStatusDto>> GetTwoFactorStatus()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        return Ok(new TwoFactorStatusDto(
            user.TwoFactorEnabled,
            await _userManager.CountRecoveryCodesAsync(user)));
    }

    // POST /api/account/2fa/setup - genereaza o cheie noua si codul QR pentru Google Authenticator.
    // 2FA nu e activ inca: se activeaza abia dupa ce utilizatorul confirma un cod din aplicatie.
    [HttpPost("2fa/setup")]
    public async Task<ActionResult<TwoFactorSetupDto>> SetupTwoFactor()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        if (user.TwoFactorEnabled)
        {
            return Conflict(new { message = "Autentificarea in doi pasi este deja activa." });
        }

        await _userManager.ResetAuthenticatorKeyAsync(user);
        var key = (await _userManager.GetAuthenticatorKeyAsync(user))!;

        // Formatul standard citit de aplicatiile de tip Authenticator
        var uri = $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(user.Email!)}" +
                  $"?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data).GetGraphic(6);

        return Ok(new TwoFactorSetupDto(
            FormatKey(key),
            $"data:image/png;base64,{Convert.ToBase64String(png)}"));
    }

    // POST /api/account/2fa/enable - verifica primul cod din aplicatie si activeaza 2FA.
    // Raspunde cu codurile de recuperare (afisate o singura data).
    [HttpPost("2fa/enable")]
    public async Task<ActionResult<RecoveryCodesDto>> EnableTwoFactor(TwoFactorCodeRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(request.Code));

        if (!valid)
        {
            return BadRequest(new { message = "Cod invalid. Verifica ora telefonului si incearca din nou." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        var codes = await _userManager.GenerateNewTwoFactorRecoveryCodesAsync(user, RecoveryCodesCount);

        return Ok(new RecoveryCodesDto(codes!.ToList()));
    }

    // POST /api/account/2fa/disable - dezactiveaza 2FA, cu un cod curent din aplicatie
    [HttpPost("2fa/disable")]
    public async Task<IActionResult> DisableTwoFactor(TwoFactorCodeRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return Unauthorized();
        }

        if (!user.TwoFactorEnabled)
        {
            return Conflict(new { message = "Autentificarea in doi pasi nu este activa." });
        }

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, NormalizeCode(request.Code));

        if (!valid)
        {
            return BadRequest(new { message = "Cod invalid." });
        }

        await _userManager.SetTwoFactorEnabledAsync(user, false);
        await _userManager.ResetAuthenticatorKeyAsync(user);

        return Ok(new { message = "Autentificarea in doi pasi a fost dezactivata." });
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        return userId is null ? null : await _userManager.FindByIdAsync(userId);
    }

    private static string NormalizeCode(string code) => code.Replace(" ", string.Empty).Replace("-", string.Empty);

    // "ABCDEFGH..." => "abcd efgh ..." (mai usor de tastat manual)
    private static string FormatKey(string key) =>
        string.Join(' ', key.ToLowerInvariant().Chunk(4).Select(c => new string(c)));
}
