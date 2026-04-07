namespace CollaborationEngine.API.Services;

public interface IChatBotService
{
    Task<string> GetBotResponseAsync(string userMessage, string collaborationId, int applicationId);
    Task<bool> IsBotEnabledAsync(int applicationId);
    Task<string> EscalateToAgentAsync(string collaborationId);
}
