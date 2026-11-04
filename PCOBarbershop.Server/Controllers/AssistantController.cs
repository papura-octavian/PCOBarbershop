using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Services;

namespace PCOBarbershop.Server.Controllers;

[ApiController]
[Route("api/[controller]")]   // => /api/assistant
public class AssistantController : ControllerBase
{
    // Keeps requests small: the local model has a limited context window
    private const int MaxMessages = 20;
    private const int MaxMessageLength = 1000;

    private readonly IAssistantService _assistant;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(IAssistantService assistant, ILogger<AssistantController> logger)
    {
        _assistant = assistant;
        _logger = logger;
    }

    // POST /api/assistant/chat - only logged-in clients (protects the model from anonymous spam,
    // and booking from chat will need to know who the client is)
    [Authorize(Roles = "Client")]
    [HttpPost("chat")]
    public async Task<ActionResult<AssistantChatResponse>> Chat(AssistantChatRequest request, CancellationToken cancellationToken)
    {
        var messages = request.Messages;

        if (messages.Count == 0 || messages.Count > MaxMessages)
        {
            return BadRequest(new { message = $"Send between 1 and {MaxMessages} messages." });
        }

        // "system" is not accepted: otherwise a user could send their own rules to the model
        if (messages.Any(m => m.Role is not ("user" or "assistant")))
        {
            return BadRequest(new { message = "Role must be 'user' or 'assistant'." });
        }

        if (messages.Any(m => string.IsNullOrWhiteSpace(m.Content) || m.Content.Length > MaxMessageLength))
        {
            return BadRequest(new { message = $"Each message must have between 1 and {MaxMessageLength} characters." });
        }

        if (messages[^1].Role != "user")
        {
            return BadRequest(new { message = "The last message must be from the user." });
        }

        // The client comes from the TOKEN: the assistant can only book for the logged-in user
        var clientId = User.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (clientId is null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await _assistant.ChatAsync(clientId, messages, cancellationToken));
        }
        catch (HttpRequestException ex)
        {
            // Ollama is not running or not reachable
            _logger.LogError(ex, "The AI model could not be reached");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new { message = "The assistant is not available right now." });
        }
    }
}
