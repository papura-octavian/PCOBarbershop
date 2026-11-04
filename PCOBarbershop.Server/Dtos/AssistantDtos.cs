namespace PCOBarbershop.Server.Dtos;

// One message from the chat. Role is "user" or "assistant" (never "system": only the server writes that).
public record AssistantMessageDto(string Role, string Content);

// The model is stateless, so the client sends the whole conversation every time
public record AssistantChatRequest(List<AssistantMessageDto> Messages);

// PendingPaymentAppointmentId is set when the assistant just created a booking:
// the Angular app then starts the Stripe payment for it, like the booking page does
public record AssistantChatResponse(string Reply, int? PendingPaymentAppointmentId = null);
