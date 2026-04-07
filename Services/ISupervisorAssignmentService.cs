using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public interface ISupervisorAssignmentService
{
    Task<Supervisor?> GetAvailableSupervisorAsync(int applicationId, int excludeAgentId);
    Task<bool> AssignSupervisorToSessionAsync(int supervisorId, int agentId, string collaborationId);
    Task<bool> ReleaseSupervisorAsync(int supervisorId);
    Task<List<Supervisor>> GetAvailableSupervisorsAsync(int applicationId, int? currentAgentId = null);
}
