using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public interface IAgentAssignmentService
{
    Task<Agent?> GetAvailableAgentAsync(int applicationId);
    Task<bool> AssignAgentToSessionAsync(int agentId, string collaborationId);
    Task<bool> ReleaseAgentAsync(int agentId);
    Task<List<Agent>> GetAvailableAgentsAsync(int applicationId);
}
