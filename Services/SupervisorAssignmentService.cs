using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public class SupervisorAssignmentService : ISupervisorAssignmentService
{
    private readonly ILocalDataService _dataService;
    private static readonly Dictionary<int, int> _supervisorAgentMapping = new();
    private static readonly object _lock = new object();

    public SupervisorAssignmentService(ILocalDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<Supervisor?> GetAvailableSupervisorAsync(int applicationId, int excludeAgentId)
    {
        lock (_lock)
        {
            var supervisors = _dataService.GetSupervisorsAsync().Result
                .Where(s => s.ApplicationId == applicationId && 
                           s.IsActive && 
                           s.IsAvailable && 
                           (!_supervisorAgentMapping.ContainsKey(s.Id) || _supervisorAgentMapping[s.Id] == excludeAgentId))
                .OrderBy(s => s.LastActiveAt)
                .ToList();

            if (supervisors.Any())
            {
                return supervisors.First();
            }
        }

        return null;
    }

    public async Task<bool> AssignSupervisorToSessionAsync(int supervisorId, int agentId, string collaborationId)
    {
        lock (_lock)
        {
            var supervisor = _dataService.GetSupervisorAsync(supervisorId).Result;
            if (supervisor == null || !supervisor.IsAvailable)
            {
                return false;
            }

            // Check if supervisor is already assigned to a different agent
            if (_supervisorAgentMapping.ContainsKey(supervisorId) && _supervisorAgentMapping[supervisorId] != agentId)
            {
                return false;
            }

            _supervisorAgentMapping[supervisorId] = agentId;
            supervisor.IsAvailable = false;
            supervisor.LastActiveAt = DateTime.UtcNow;
            supervisor.UpdatedAt = DateTime.UtcNow;

            // Create supervisor session record
            var collaborationSession = _dataService.GetCollaborationSessionAsync(collaborationId).Result;

            if (collaborationSession != null)
            {
                var supervisorSession = new AgentSupervisorSession
                {
                    CollaborationSessionId = collaborationSession.Id,
                    AgentId = agentId,
                    SupervisorId = supervisorId,
                    JoinedAt = DateTime.UtcNow,
                    IsActive = true
                };

                _dataService.SaveAgentSupervisorSessionAsync(supervisorSession).Wait();
            }

            _dataService.SaveSupervisorAsync(supervisor).Wait();
            return true;
        }
    }

    public async Task<bool> ReleaseSupervisorAsync(int supervisorId)
    {
        lock (_lock)
        {
            if (!_supervisorAgentMapping.ContainsKey(supervisorId))
            {
                return false;
            }

            _supervisorAgentMapping.Remove(supervisorId);

            var supervisor = _dataService.GetSupervisorAsync(supervisorId).Result;
            if (supervisor != null)
            {
                supervisor.IsAvailable = true;
                supervisor.LastActiveAt = DateTime.UtcNow;
                supervisor.UpdatedAt = DateTime.UtcNow;

                // Update active supervisor sessions
                var allSessions = _dataService.GetAgentSupervisorSessionsAsync().Result;
                var activeSessions = allSessions
                    .Where(ass => ass.SupervisorId == supervisorId && ass.IsActive)
                    .ToList();

                foreach (var session in activeSessions)
                {
                    session.LeftAt = DateTime.UtcNow;
                    session.IsActive = false;
                    _dataService.SaveAgentSupervisorSessionAsync(session).Wait();
                }

                _dataService.SaveSupervisorAsync(supervisor).Wait();
            }

            return true;
        }
    }

    public async Task<List<Supervisor>> GetAvailableSupervisorsAsync(int applicationId, int? currentAgentId = null)
    {
        lock (_lock)
        {
            var supervisors = _dataService.GetSupervisorsAsync().Result
                .Where(s => s.ApplicationId == applicationId && 
                           s.IsActive && 
                           s.IsAvailable);

            if (currentAgentId.HasValue)
            {
                supervisors = supervisors.Where(s => !_supervisorAgentMapping.ContainsKey(s.Id) || _supervisorAgentMapping[s.Id] == currentAgentId.Value);
            }
            else
            {
                supervisors = supervisors.Where(s => !_supervisorAgentMapping.ContainsKey(s.Id));
            }

            return supervisors.OrderBy(s => s.Name).ToList();
        }
    }
}
