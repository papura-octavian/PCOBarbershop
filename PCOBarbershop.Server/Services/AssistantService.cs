using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using PCOBarbershop.Server.Data;
using PCOBarbershop.Server.Dtos;
using PCOBarbershop.Server.Settings;

namespace PCOBarbershop.Server.Services;

public interface IAssistantService
{
    Task<AssistantChatResponse> ChatAsync(string clientId, IReadOnlyList<AssistantMessageDto> messages, CancellationToken cancellationToken);
}

public class AssistantService : IAssistantService
{
    private readonly IChatClient _chatClient;
    private readonly AssistantTools _tools;
    private readonly AppDbContext _db;
    private readonly StripeSettings _stripeSettings;

    public AssistantService(IChatClient chatClient, AssistantTools tools, AppDbContext db, IOptions<StripeSettings> stripeOptions)
    {
        _chatClient = chatClient;
        _tools = tools;
        _db = db;
        _stripeSettings = stripeOptions.Value;
    }

    public async Task<AssistantChatResponse> ChatAsync(string clientId, IReadOnlyList<AssistantMessageDto> messages, CancellationToken cancellationToken)
    {
        // Who the booking is for comes from the token, never from the conversation
        _tools.ClientId = clientId;

        // The system prompt is always built here, on the server, never taken from the client
        var history = new List<ChatMessage>
        {
            new(ChatRole.System, await BuildSystemPromptAsync())
        };

        foreach (var message in messages)
        {
            var role = message.Role == "assistant" ? ChatRole.Assistant : ChatRole.User;
            history.Add(new ChatMessage(role, message.Content));
        }

        var options = new ChatOptions { Tools = _tools.GetTools() };

        var response = await _chatClient.GetResponseAsync(history, options, cancellationToken);

        // If create_booking ran, Angular starts the payment for that appointment
        return new AssistantChatResponse(response.Text, _tools.CreatedAppointmentId);
    }

    // Gives the model the context it cannot know: today's date, the real barbers and services, and the rules
    private async Task<string> BuildSystemPromptAsync()
    {
        var today = SalonTime.Today;

        var barbers = await _db.Barbers
            .AsNoTracking()
            .OrderBy(b => b.DisplayName)
            .Select(b => b.DisplayName)
            .ToListAsync();

        var services = await _db.Services
            .AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync();

        var prompt = new StringBuilder();
        prompt.AppendLine("You are the booking assistant of PCO Barbershop.");
        prompt.AppendLine($"Today is {today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)} ({today.DayOfWeek}).");
        prompt.AppendLine();
        prompt.AppendLine($"Barbers: {string.Join(", ", barbers)}.");
        prompt.AppendLine("Services:");
        foreach (var service in services)
        {
            prompt.AppendLine($"- {service.Name}: {service.DurationMinutes} min, {service.Price.ToString(CultureInfo.InvariantCulture)} RON");
        }
        prompt.AppendLine();
        prompt.AppendLine("Rules:");
        prompt.AppendLine("- To know when a barber is free, always call get_free_slots. Never invent times.");
        prompt.AppendLine("- If the user did not say the barber, the service or the day, ask for the missing part first.");
        prompt.AppendLine("- If the requested time is not free, suggest the closest free times.");
        prompt.AppendLine("- To book: check the time with get_free_slots, then summarize the booking (barber, service, day, time, price) and ask the user to confirm.");
        prompt.AppendLine("- Call create_booking only when the user's last message clearly confirms that exact booking (for example \"yes\" or \"da\"). Even if the user says you do not need to ask, still ask for confirmation once.");
        prompt.AppendLine($"- After create_booking succeeds, the app opens the card payment page by itself. The booking is held for {_stripeSettings.PaymentTimeoutMinutes} minutes and is cancelled if it is not paid.");
        prompt.AppendLine("- Answer in the same language as the user. Keep answers short.");

        return prompt.ToString();
    }
}
