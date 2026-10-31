using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Models;
using PCOBarbershop.Server.Services;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITokenService _tokenService;
    private const string GoogleProvider = "Google";

    private readonly IEmailService _emailService;
    private readonly IGoogleTokenValidator _google;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        ITokenService tokenService,
        IEmailService emailService,
        IGoogleTokenValidator google,
        ILogger<AuthController> logger)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _emailService = emailService;
        _google = google;
        _logger = logger;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });
        }

        await _userManager.AddToRoleAsync(user, "Client");

        // Contul nu poate fi folosit pana nu e confirmat cu codul primit pe e-mail
        await SendConfirmationCodeAsync(user);

        return Ok(new { message = "Cont creat. Ti-am trimis pe e-mail un cod de confirmare.", email = user.Email });
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);

        // Acelasi mesaj pentru e-mail inexistent si parola gresita,
        // ca sa nu poata nimeni afla ce e-mailuri au cont.
        if (user is null)
        {
            return Unauthorized(new { message = "E-mail sau parola incorecte." });
        }

        // Dupa 5 incercari gresite contul se blocheaza cateva minute (protectie la ghicirea parolei)
        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = "Prea multe incercari gresite. Incearca din nou peste cateva minute." });
        }

        if (!await _userManager.CheckPasswordAsync(user, request.Password))
        {
            await _userManager.AccessFailedAsync(user);
            return Unauthorized(new { message = "E-mail sau parola incorecte." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        if (!user.EmailConfirmed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Confirma mai intai adresa de e-mail cu codul primit.",
                requiresEmailConfirmation = true,
                email = user.Email
            });
        }

        // Pasul 2 al autentificarii: contul are 2FA, deci cerem si codul din Google Authenticator
        if (user.TwoFactorEnabled)
        {
            return Ok(new TwoFactorRequiredResponse(true, _tokenService.CreateTwoFactorToken(user)));
        }

        return Ok(await CreateAuthResponseAsync(user));
    }

    // POST /api/auth/login-2fa - al doilea pas al autentificarii: codul TOTP (sau un cod de recuperare)
    [HttpPost("login-2fa")]
    public async Task<ActionResult<AuthResponse>> LoginTwoFactor(LoginTwoFactorRequest request)
    {
        var userId = await _tokenService.ValidateTwoFactorTokenAsync(request.TwoFactorToken);
        var user = userId is null ? null : await _userManager.FindByIdAsync(userId);

        if (user is null || !user.TwoFactorEnabled)
        {
            return Unauthorized(new { message = "Sesiunea de autentificare a expirat. Autentifica-te din nou." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = "Prea multe incercari gresite. Incearca din nou peste cateva minute." });
        }

        var code = request.Code.Replace(" ", string.Empty).Replace("-", string.Empty);

        var valid = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, code);

        // Daca nu e un cod din aplicatie, poate fi unul dintre codurile de recuperare (se folosesc o singura data)
        if (!valid)
        {
            valid = (await _userManager.RedeemTwoFactorRecoveryCodeAsync(user, request.Code.Trim())).Succeeded;
        }

        if (!valid)
        {
            await _userManager.AccessFailedAsync(user);
            return BadRequest(new { message = "Cod invalid." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        return Ok(await CreateAuthResponseAsync(user));
    }

    // GET /api/auth/google/config - Angular afla de aici daca si cu ce Client ID afiseaza butonul Google
    [HttpGet("google/config")]
    public ActionResult<GoogleConfigDto> GoogleConfig()
    {
        return Ok(new GoogleConfigDto(_google.IsConfigured, _google.ClientId));
    }

    // POST /api/auth/google - autentificare cu contul Google (OAuth 2.0 / OpenID Connect).
    // La prima autentificare se creeaza automat un cont de client, deja confirmat.
    [HttpPost("google")]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request)
    {
        if (!_google.IsConfigured)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "Autentificarea cu Google nu este configurata pe server." });
        }

        var googleUser = await _google.ValidateAsync(request.IdToken);
        if (googleUser is null)
        {
            return Unauthorized(new { message = "Autentificarea cu Google a esuat." });
        }

        if (!googleUser.EmailVerified)
        {
            return Unauthorized(new { message = "Adresa de e-mail a contului Google nu este verificata." });
        }

        // 1. Contul deja legat de acest cont Google
        var user = await _userManager.FindByLoginAsync(GoogleProvider, googleUser.Subject);

        if (user is null)
        {
            // 2. Un cont existent cu acelasi e-mail (Google a verificat e-mailul, deci il putem lega)
            user = await _userManager.FindByEmailAsync(googleUser.Email);

            if (user is null)
            {
                // 3. Cont nou de client, fara parola
                user = new ApplicationUser
                {
                    UserName = googleUser.Email,
                    Email = googleUser.Email,
                    EmailConfirmed = true,
                    FirstName = googleUser.FirstName,
                    LastName = googleUser.LastName
                };

                var created = await _userManager.CreateAsync(user);
                if (!created.Succeeded)
                {
                    return BadRequest(new { errors = created.Errors.Select(e => e.Description) });
                }

                await _userManager.AddToRoleAsync(user, "Client");
            }

            await _userManager.AddLoginAsync(user, new UserLoginInfo(GoogleProvider, googleUser.Subject, "Google"));
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new { message = "Contul este blocat temporar. Incearca din nou peste cateva minute." });
        }

        // E-mailul a fost verificat de Google
        if (!user.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        if (user.TwoFactorEnabled)
        {
            return Ok(new TwoFactorRequiredResponse(true, _tokenService.CreateTwoFactorToken(user)));
        }

        return Ok(await CreateAuthResponseAsync(user));
    }

    // POST /api/auth/confirm-email - confirma contul cu codul de 6 cifre si autentifica utilizatorul
    [HttpPost("confirm-email")]
    public async Task<ActionResult<AuthResponse>> ConfirmEmail(ConfirmEmailRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return BadRequest(new { message = "Cod invalid sau expirat." });
        }

        if (user.EmailConfirmed)
        {
            return BadRequest(new { message = "Contul este deja confirmat. Te poti autentifica." });
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return BadRequest(new { message = "Prea multe incercari gresite. Incearca din nou peste cateva minute." });
        }

        var result = await _userManager.ConfirmEmailAsync(user, request.Code.Trim());
        if (!result.Succeeded)
        {
            await _userManager.AccessFailedAsync(user);
            return BadRequest(new { message = "Cod invalid sau expirat." });
        }

        await _userManager.ResetAccessFailedCountAsync(user);

        return Ok(await CreateAuthResponseAsync(user));
    }

    // POST /api/auth/resend-confirmation - trimite un cod nou.
    // Raspunsul e mereu acelasi, ca sa nu se poata afla ce e-mailuri au cont.
    [HttpPost("resend-confirmation")]
    public async Task<IActionResult> ResendConfirmation(ResendConfirmationRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is not null && !user.EmailConfirmed)
        {
            await SendConfirmationCodeAsync(user);
        }

        return Ok(new { message = "Daca exista un cont neconfirmat cu acest e-mail, am trimis un cod nou." });
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
    {
        return Ok(new
        {
            id = User.FindFirstValue(JwtRegisteredClaimNames.Sub),
            email = User.FindFirstValue(JwtRegisteredClaimNames.Email),
            roles = User.FindAll("role").Select(c => c.Value)
        });
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var token = _tokenService.CreateToken(user, roles);

        return new AuthResponse(token.Token, token.ExpiresAtUtc, user.Email!, roles);
    }

    private async Task SendConfirmationCodeAsync(ApplicationUser user)
    {
        // Cod de 6 cifre generat de Identity (TOTP pe baza security stamp-ului), valabil cateva minute
        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);

        try
        {
            await _emailService.SendAsync(
                user.Email!,
                $"{user.FirstName} {user.LastName}",
                "Codul tau de confirmare PCO Barbershop",
                EmailTemplates.ConfirmationCode(user.FirstName, code));
        }
        catch (Exception ex)
        {
            // Contul ramane creat; utilizatorul poate cere un cod nou
            _logger.LogError(ex, "Nu s-a putut trimite codul de confirmare catre {Email}", user.Email);
        }
    }
}
