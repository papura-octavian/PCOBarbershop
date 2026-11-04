// One chat message, in the format the server expects (POST /api/assistant/chat)
export interface AssistantMessage {
  role: 'user' | 'assistant';
  content: string;
}

export interface AssistantChatResponse {
  reply: string;
  // Set when the assistant just booked an appointment: the app then opens the Stripe payment
  pendingPaymentAppointmentId: number | null;
}
