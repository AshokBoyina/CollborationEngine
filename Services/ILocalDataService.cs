using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public interface ILocalDataService
{
    Task<List<Application>> GetApplicationsAsync();
    Task<Application?> GetApplicationAsync(int id);
    Task<Application> SaveApplicationAsync(Application application);
    Task<List<User>> GetUsersAsync();
    Task<User?> GetUserAsync(int id);
    Task<User> SaveUserAsync(User user);
    Task<List<Agent>> GetAgentsAsync();
    Task<Agent?> GetAgentAsync(int id);
    Task<Agent> SaveAgentAsync(Agent agent);
    Task<List<Supervisor>> GetSupervisorsAsync();
    Task<Supervisor?> GetSupervisorAsync(int id);
    Task<Supervisor> SaveSupervisorAsync(Supervisor supervisor);
    Task<List<CollaborationSession>> GetCollaborationSessionsAsync();
    Task<CollaborationSession?> GetCollaborationSessionAsync(string collaborationId);
    Task<CollaborationSession> SaveCollaborationSessionAsync(CollaborationSession session);
    Task<List<ChatMessage>> GetChatMessagesAsync(int sessionId);
    Task<ChatMessage> SaveChatMessageAsync(ChatMessage message);
    Task<List<AgentSupervisorSession>> GetAgentSupervisorSessionsAsync();
    Task<AgentSupervisorSession> SaveAgentSupervisorSessionAsync(AgentSupervisorSession session);
}
