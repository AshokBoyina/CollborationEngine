using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public class AgentAssignmentService : IAgentAssignmentService
{
    private readonly ILocalDataService _dataService;
    private static readonly Dictionary<int, string> _agentSessions = new();
    private static readonly object _lock = new object();
    private static readonly Random _rng = Random.Shared;

    public AgentAssignmentService(ILocalDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<Agent?> GetAvailableAgentAsync(int applicationId)
    {
        lock (_lock)
        {
            var agents = _dataService.GetAgentsAsync().Result
                .Where(a => a.ApplicationId == applicationId && 
                           a.IsActive && 
                           a.IsAvailable && 
                           !_agentSessions.ContainsKey(a.Id))
                .ToList();

            if (agents.Any())
            {
                var idx = _rng.Next(agents.Count);
                return agents[idx];
            }
        }

        return null;
    }

    public async Task<bool> AssignAgentToSessionAsync(int agentId, string collaborationId)
    {
        lock (_lock)
        {
            if (_agentSessions.ContainsKey(agentId))
            {
                return false; // Agent already assigned
            }

            var agent = _dataService.GetAgentAsync(agentId).Result;
            if (agent == null || !agent.IsAvailable)
            {
                return false;
            }

            _agentSessions[agentId] = collaborationId;
            agent.IsAvailable = false;
            agent.LastActiveAt = DateTime.UtcNow;
            agent.UpdatedAt = DateTime.UtcNow;

            _dataService.SaveAgentAsync(agent).Wait();
            return true;
        }
    }

    public async Task<bool> ReleaseAgentAsync(int agentId)
    {
        lock (_lock)
        {
            if (!_agentSessions.ContainsKey(agentId))
            {
                return false;
            }

            _agentSessions.Remove(agentId);

            var agent = _dataService.GetAgentAsync(agentId).Result;
            if (agent != null)
            {
                agent.IsAvailable = true;
                agent.LastActiveAt = DateTime.UtcNow;
                agent.UpdatedAt = DateTime.UtcNow;
                _dataService.SaveAgentAsync(agent).Wait();
            }

            return true;
        }
    }

    public async Task<List<Agent>> GetAvailableAgentsAsync(int applicationId)
    {
        lock (_lock)
        {
            return _dataService.GetAgentsAsync().Result
                .Where(a => a.ApplicationId == applicationId && 
                           a.IsActive && 
                           a.IsAvailable && 
                           !_agentSessions.ContainsKey(a.Id))
                .OrderBy(a => a.Name)
                .ToList();
        }
    }
}
