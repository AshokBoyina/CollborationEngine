using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public interface ICollaborationService
{
    Task<CollaborationSession> CreateSessionAsync(int userId, int applicationId);
    Task<CollaborationSession?> GetSessionAsync(string collaborationId);
    Task<CollaborationSession?> AssignAgentAsync(string collaborationId);
    Task<ChatMessage> AddMessageAsync(string collaborationId, string content, string senderType, int? senderId);
    Task<List<ChatMessage>> GetChatHistoryAsync(string collaborationId);
    Task EndSessionAsync(string collaborationId);
    Task<List<Supervisor>> GetAvailableSupervisorsAsync(int applicationId, int agentId);
}
