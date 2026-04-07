namespace CollaborationEngine.API.Services;

public class ChatBotService : IChatBotService
{
    public async Task<string> GetBotResponseAsync(string userMessage, string collaborationId, int applicationId)
    {
        // Demo bot responses
        var responses = new List<string>
        {
            "Thank you for your message. I'm here to help you. Could you please provide more details about your issue?",
            "I understand you need assistance. Let me help you resolve this issue. What specific problem are you experiencing?",
            "I'm here to support you. Please tell me more about what you need help with, and I'll do my best to assist you.",
            "Thank you for reaching out. I'm ready to help you. Could you describe your situation in more detail?"
        };

        var random = new Random();
        var response = responses[random.Next(responses.Count)];

        // Check if escalation is needed
        if (ShouldEscalateToAgent(userMessage))
        {
            response += "\n\nI think it would be best to connect you with a human agent who can better assist you with this matter. Would you like me to do that?";
        }

        return await Task.FromResult(response);
    }

    public async Task<bool> IsBotEnabledAsync(int applicationId)
    {
        // For demo, bot is enabled for all applications
        return await Task.FromResult(true);
    }

    public async Task<string> EscalateToAgentAsync(string collaborationId)
    {
        return await Task.FromResult("I'm connecting you with a human agent now. Please wait a moment while I find the best available agent to assist you.");
    }

    private bool ShouldEscalateToAgent(string message)
    {
        var escalateKeywords = new[]
        {
            "human agent", "speak to human", "talk to person", "real person", "supervisor",
            "manager", "complaint", "urgent", "emergency", "cancel", "refund", "billing",
            "legal", "security", "fraud", "account locked", "password reset"
        };

        var messageLower = message.ToLower();
        return escalateKeywords.Any(keyword => messageLower.Contains(keyword));
    }
}
