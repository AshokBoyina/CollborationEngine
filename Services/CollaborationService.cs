using CollaborationEngine.API.Models;

namespace CollaborationEngine.API.Services;

public class CollaborationService : ICollaborationService
{
    private readonly ILocalDataService _dataService;
    private readonly IAgentAssignmentService _agentAssignmentService;
    private readonly ISupervisorAssignmentService _supervisorAssignmentService;
    private readonly IChatBotService _chatBotService;

    public CollaborationService(
        ILocalDataService dataService,
        IAgentAssignmentService agentAssignmentService,
        ISupervisorAssignmentService supervisorAssignmentService,
        IChatBotService chatBotService)
    {
        _dataService = dataService;
        _agentAssignmentService = agentAssignmentService;
        _supervisorAssignmentService = supervisorAssignmentService;
        _chatBotService = chatBotService;
    }

    public async Task<CollaborationSession> CreateSessionAsync(int userId, int applicationId)
    {
        var collaborationId = Guid.NewGuid().ToString("N")[..16];
        
        var session = new CollaborationSession
        {
            CollaborationId = collaborationId,
            Status = "Active",
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            UserId = userId,
            ApplicationId = applicationId
        };

        try
        {
            session = await _dataService.SaveCollaborationSessionAsync(session);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to persist collaboration session (id={collaborationId}) due to save failure", ex);
        }

        CollaborationSession? persisted;
        try
        {
            persisted = await _dataService.GetCollaborationSessionAsync(collaborationId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to persist collaboration session (id={collaborationId}) due to read/parse failure", ex);
        }

        if (persisted == null)
        {
            var sessions = await _dataService.GetCollaborationSessionsAsync();
            var sampleIds = sessions.Select(s => s.CollaborationId).Where(id => !string.IsNullOrWhiteSpace(id)).Take(5).ToList();
            throw new InvalidOperationException($"Failed to persist collaboration session (id={collaborationId}, knownSessions={sessions.Count}, sample=[{string.Join(",", sampleIds)}])");
        }

        // Add welcome message (save directly to avoid needing to re-load session by collaborationId)
        await _dataService.SaveChatMessageAsync(new ChatMessage
        {
            Content = "Welcome to the collaboration session! How can we help you today?",
            MessageType = "Text",
            SentAt = DateTime.UtcNow,
            CollaborationSessionId = session.Id,
            SenderId = null,
            SenderType = "System"
        });

        return session;
    }

    public async Task<CollaborationSession?> GetSessionAsync(string collaborationId)
    {
        var session = await _dataService.GetCollaborationSessionAsync(collaborationId);
        if (session == null)
        {
            return null;
        }

        // Hydrate navigation properties (demo/local mode)
        session.User = await _dataService.GetUserAsync(session.UserId) ?? new User { Id = session.UserId };
        session.Application = await _dataService.GetApplicationAsync(session.ApplicationId) ?? new Application { Id = session.ApplicationId };
        session.Agent = session.AgentId.HasValue ? await _dataService.GetAgentAsync(session.AgentId.Value) : null;

        var messages = await _dataService.GetChatMessagesAsync(session.Id);
        session.ChatMessages = messages.OrderBy(m => m.SentAt).ToList();

        var supervisorSessions = await _dataService.GetAgentSupervisorSessionsAsync();
        session.SupervisorSessions = supervisorSessions
            .Where(s => s.CollaborationSessionId == session.Id)
            .ToList();

        return session;
    }

    public async Task<CollaborationSession?> AssignAgentAsync(string collaborationId)
    {
        var session = await _dataService.GetCollaborationSessionAsync(collaborationId);

        if (session == null || session.AgentId != null)
        {
            return session == null ? null : await GetSessionAsync(collaborationId);
        }

        var availableAgent = await _agentAssignmentService.GetAvailableAgentAsync(session.ApplicationId);
        if (availableAgent == null)
        {
            return await GetSessionAsync(collaborationId);
        }

        var assigned = await _agentAssignmentService.AssignAgentToSessionAsync(availableAgent.Id, collaborationId);
        if (assigned)
        {
            session.AgentId = availableAgent.Id;
            session.UpdatedAt = DateTime.UtcNow;
            await _dataService.SaveCollaborationSessionAsync(session);

            // Add system message
            await AddMessageAsync(collaborationId, $"Agent {availableAgent.Name} has joined the session", "System", null);
        }

        return await GetSessionAsync(collaborationId);
    }

    public async Task<ChatMessage> AddMessageAsync(string collaborationId, string content, string senderType, int? senderId)
    {
        var session = await _dataService.GetCollaborationSessionAsync(collaborationId);

        if (session == null)
        {
            throw new InvalidOperationException("Collaboration session not found");
        }

        var message = new ChatMessage
        {
            Content = content,
            MessageType = senderType == "Bot" ? "Bot" : "Text",
            SentAt = DateTime.UtcNow,
            CollaborationSessionId = session.Id,
            SenderId = senderId,
            SenderType = senderType
        };

        message = await _dataService.SaveChatMessageAsync(message);

        return message;
    }

    public async Task<List<ChatMessage>> GetChatHistoryAsync(string collaborationId)
    {
        var session = await _dataService.GetCollaborationSessionAsync(collaborationId);

        if (session == null)
        {
            return new List<ChatMessage>();
        }

        var messages = await _dataService.GetChatMessagesAsync(session.Id);
        return messages.OrderBy(m => m.SentAt).ToList();
    }

    public async Task EndSessionAsync(string collaborationId)
    {
        var session = await _dataService.GetCollaborationSessionAsync(collaborationId);

        if (session != null)
        {
            session.Status = "Ended";
            session.EndedAt = DateTime.UtcNow;
            session.UpdatedAt = DateTime.UtcNow;

            // Release agent and supervisors
            if (session.AgentId.HasValue)
            {
                await _agentAssignmentService.ReleaseAgentAsync(session.AgentId.Value);
            }

            var allSupervisorSessions = await _dataService.GetAgentSupervisorSessionsAsync();
            var activeSessions = allSupervisorSessions
                .Where(s => s.CollaborationSessionId == session.Id && s.IsActive)
                .ToList();

            foreach (var supervisorSession in activeSessions)
            {
                await _supervisorAssignmentService.ReleaseSupervisorAsync(supervisorSession.SupervisorId);
                supervisorSession.LeftAt = DateTime.UtcNow;
                supervisorSession.IsActive = false;
                await _dataService.SaveAgentSupervisorSessionAsync(supervisorSession);
            }

            await _dataService.SaveCollaborationSessionAsync(session);
        }
    }

    public async Task<List<Supervisor>> GetAvailableSupervisorsAsync(int applicationId, int agentId)
    {
        return await _supervisorAssignmentService.GetAvailableSupervisorsAsync(applicationId, agentId);
    }
}
