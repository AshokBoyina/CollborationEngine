using CollaborationEngine.API.Models;
using CollaborationEngine.API.Services;
using Microsoft.AspNetCore.SignalR;

namespace CollaborationEngine.API.Hubs;

public class CollaborationHub : Hub
{
    private readonly ILocalDataService _dataService;
    private readonly ICollaborationService _collaborationService;
    private readonly IAgentAssignmentService _agentAssignmentService;
    private readonly ISupervisorAssignmentService _supervisorAssignmentService;
    private readonly IStreamingService _streamingService;
    private readonly IChatBotService _chatBotService;
    private readonly ILogger<CollaborationHub> _logger;

    private static readonly Dictionary<string, HashSet<string>> _connectionGroups = new();
    private static readonly Dictionary<string, string> _connectionUserTypes = new();
    private static readonly Dictionary<string, string> _connectionUserIds = new();
    private static readonly Dictionary<int, HashSet<string>> _agentConnections = new();
    private static readonly object _agentConnectionsLock = new();
    private static readonly Dictionary<int, HashSet<string>> _supervisorConnections = new();
    private static readonly object _supervisorConnectionsLock = new();

    public CollaborationHub(
        ILocalDataService dataService,
        ICollaborationService collaborationService,
        IAgentAssignmentService agentAssignmentService,
        ISupervisorAssignmentService supervisorAssignmentService,
        IStreamingService streamingService,
        IChatBotService chatBotService,
        ILogger<CollaborationHub> logger)
    {
        _dataService = dataService;
        _collaborationService = collaborationService;
        _agentAssignmentService = agentAssignmentService;
        _supervisorAssignmentService = supervisorAssignmentService;
        _streamingService = streamingService;
        _chatBotService = chatBotService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        // Demo mode: extract identity from query string
        var userTypeRaw = Context.GetHttpContext()?.Request.Query["userType"].ToString();
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        var userType = NormalizeUserType(userTypeRaw);

        if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(userType))
        {
            _connectionUserTypes[Context.ConnectionId] = userType;
            _connectionUserIds[Context.ConnectionId] = userId;
            _logger.LogInformation($"User {userId} of type {userType} connected");

            if (userType == "Agent" && int.TryParse(userId, out var agentId))
            {
                lock (_agentConnectionsLock)
                {
                    if (!_agentConnections.TryGetValue(agentId, out var connections))
                    {
                        connections = new HashSet<string>();
                        _agentConnections[agentId] = connections;
                    }

                    connections.Add(Context.ConnectionId);
                }

                try
                {
                    var sessions = await _dataService.GetCollaborationSessionsAsync();
                    var assigned = sessions
                        .Where(s => s.AgentId == agentId && string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase) && s.EndedAt == null)
                        .OrderByDescending(s => s.StartedAt)
                        .FirstOrDefault();

                    if (assigned != null && !string.IsNullOrWhiteSpace(assigned.CollaborationId))
                    {
                        await Groups.AddToGroupAsync(Context.ConnectionId, assigned.CollaborationId);
                        await Clients.Caller.SendAsync("AssignedToSession", new
                        {
                            CollaborationId = assigned.CollaborationId,
                            AgentId = agentId
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to auto-join assigned session for agent on connect. AgentId={AgentId}", agentId);
                }
            }

            if (userType == "Supervisor" && int.TryParse(userId, out var supervisorId))
            {
                lock (_supervisorConnectionsLock)
                {
                    if (!_supervisorConnections.TryGetValue(supervisorId, out var connections))
                    {
                        connections = new HashSet<string>();
                        _supervisorConnections[supervisorId] = connections;
                    }

                    connections.Add(Context.ConnectionId);
                }
            }
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var connectionId = Context.ConnectionId;
        
        if (_connectionUserTypes.TryGetValue(connectionId, out var userType))
        {
            _connectionUserIds.TryGetValue(connectionId, out var userId);
            _logger.LogInformation($"User {userId} of type {userType} disconnected");

            if (userType == "Agent" && int.TryParse(userId, out var disconnectAgentId))
            {
                lock (_agentConnectionsLock)
                {
                    if (_agentConnections.TryGetValue(disconnectAgentId, out var conns))
                    {
                        conns.Remove(connectionId);
                        if (conns.Count == 0)
                        {
                            _agentConnections.Remove(disconnectAgentId);
                        }
                    }
                }
            }

            if (userType == "Supervisor" && int.TryParse(userId, out var disconnectSupervisorId))
            {
                lock (_supervisorConnectionsLock)
                {
                    if (_supervisorConnections.TryGetValue(disconnectSupervisorId, out var conns))
                    {
                        conns.Remove(connectionId);
                        if (conns.Count == 0)
                        {
                            _supervisorConnections.Remove(disconnectSupervisorId);
                        }
                    }
                }
            }

            // Handle agent/supervisor release on disconnect
            if (userType == "Agent" && int.TryParse(userId, out var agentId))
            {
                await _agentAssignmentService.ReleaseAgentAsync(agentId);
            }
            else if (userType == "Supervisor" && int.TryParse(userId, out var supervisorId))
            {
                await _supervisorAssignmentService.ReleaseSupervisorAsync(supervisorId);
            }
        }

        _connectionUserTypes.Remove(connectionId);
        _connectionUserIds.Remove(connectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task AddAgentToCollaborationGroupIfConnectedAsync(int agentId, string collaborationId)
    {
        List<string> agentConnectionIds;
        lock (_agentConnectionsLock)
        {
            agentConnectionIds = _agentConnections.TryGetValue(agentId, out var conns)
                ? conns.ToList()
                : new List<string>();
        }

        foreach (var connId in agentConnectionIds)
        {
            try
            {
                await Groups.AddToGroupAsync(connId, collaborationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add agent connection to group. AgentId={AgentId}, ConnectionId={ConnectionId}, CollaborationId={CollaborationId}", agentId, connId, collaborationId);
            }
        }

        if (agentConnectionIds.Count > 0)
        {
            await Clients.Clients(agentConnectionIds).SendAsync("AssignedToSession", new
            {
                CollaborationId = collaborationId,
                AgentId = agentId
            });
        }
    }

    private async Task AddSupervisorToCollaborationGroupIfConnectedAsync(int supervisorId, string collaborationId)
    {
        List<string> supervisorConnectionIds;
        lock (_supervisorConnectionsLock)
        {
            supervisorConnectionIds = _supervisorConnections.TryGetValue(supervisorId, out var conns)
                ? conns.ToList()
                : new List<string>();
        }

        foreach (var connId in supervisorConnectionIds)
        {
            try
            {
                await Groups.AddToGroupAsync(connId, collaborationId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to add supervisor connection to group. SupervisorId={SupervisorId}, ConnectionId={ConnectionId}, CollaborationId={CollaborationId}", supervisorId, connId, collaborationId);
            }
        }

        if (supervisorConnectionIds.Count > 0)
        {
            await Clients.Clients(supervisorConnectionIds).SendAsync("AssignedToSession", new
            {
                CollaborationId = collaborationId,
                SupervisorId = supervisorId
            });
        }
    }

    public async Task JoinCollaboration(string collaborationId)
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        var userType = _connectionUserTypes.GetValueOrDefault(Context.ConnectionId);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userType))
        {
            await Clients.Caller.SendAsync("Error", "Invalid user information");
            return;
        }

        if (string.IsNullOrWhiteSpace(collaborationId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id");
            return;
        }

        var session = await _collaborationService.GetSessionAsync(collaborationId);
        if (session == null)
        {
            var sessions = await _dataService.GetCollaborationSessionsAsync();
            var sampleIds = sessions.Select(s => s.CollaborationId).Where(id => !string.IsNullOrWhiteSpace(id)).Take(5).ToList();
            _logger.LogWarning("JoinCollaboration failed. CollaborationId={CollaborationId}, UserId={UserId}, UserType={UserType}, KnownSessions={Count}", collaborationId, userId, userType, sessions.Count);
            await Clients.Caller.SendAsync("Error", $"Collaboration session not found (id={collaborationId}, knownSessions={sessions.Count}, sample=[{string.Join(",", sampleIds)}])");
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, collaborationId);

        if (!_connectionGroups.ContainsKey(collaborationId))
        {
            _connectionGroups[collaborationId] = new HashSet<string>();
        }
        _connectionGroups[collaborationId].Add(Context.ConnectionId);

        await Clients.Group(collaborationId).SendAsync("UserJoined", new
        {
            UserId = userId,
            UserType = userType,
            CollaborationId = collaborationId
        });

        // Send chat history to the newly joined user
        var chatHistory = await _collaborationService.GetChatHistoryAsync(collaborationId);
        await Clients.Caller.SendAsync("ChatHistory", chatHistory);
    }

    public async Task LeaveCollaboration(string collaborationId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, collaborationId);
        
        if (_connectionGroups.ContainsKey(collaborationId))
        {
            _connectionGroups[collaborationId].Remove(Context.ConnectionId);
            if (_connectionGroups[collaborationId].Count == 0)
            {
                _connectionGroups.Remove(collaborationId);
            }
        }

        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        var userType = _connectionUserTypes.GetValueOrDefault(Context.ConnectionId);

        await Clients.Group(collaborationId).SendAsync("UserLeft", new
        {
            UserId = userId,
            UserType = userType,
            CollaborationId = collaborationId
        });
    }

    public async Task SendMessage(string collaborationId, string message)
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        var userType = _connectionUserTypes.GetValueOrDefault(Context.ConnectionId);

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userType))
        {
            await Clients.Caller.SendAsync("Error", "Invalid user information");
            return;
        }

        int? senderId = int.TryParse(userId, out var id) ? id : null;
        ChatMessage chatMessage;
        try
        {
            chatMessage = await _collaborationService.AddMessageAsync(collaborationId, message, userType, senderId);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "SendMessage failed. CollaborationId={CollaborationId}, UserId={UserId}, UserType={UserType}", collaborationId, userId, userType);
            await Clients.Caller.SendAsync("Error", ex.Message);
            return;
        }

        await Clients.Group(collaborationId).SendAsync("ReceiveMessage", new
        {
            Id = chatMessage.Id,
            Content = chatMessage.Content,
            SenderType = chatMessage.SenderType,
            SenderId = chatMessage.SenderId,
            SentAt = chatMessage.SentAt,
            MessageType = chatMessage.MessageType
        });

        // If user sends message and no agent is assigned, try to assign one
        if (userType == "User")
        {
            var session = await _collaborationService.GetSessionAsync(collaborationId);
            if (session != null && session.AgentId == null)
            {
                var assignedSession = await _collaborationService.AssignAgentAsync(collaborationId);
                if (assignedSession?.AgentId != null)
                {
                    await AddAgentToCollaborationGroupIfConnectedAsync(assignedSession.AgentId.Value, collaborationId);
                    await Clients.Group(collaborationId).SendAsync("AgentAssigned", new
                    {
                        AgentId = assignedSession.AgentId,
                        AgentName = assignedSession.Agent?.Name
                    });
                }
            }
        }
    }

    public async Task RequestAgent(string collaborationId)
    {
        var session = await _collaborationService.GetSessionAsync(collaborationId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", "Collaboration session not found");
            return;
        }

        var assignedSession = await _collaborationService.AssignAgentAsync(collaborationId);
        if (assignedSession?.AgentId != null)
        {
            await AddAgentToCollaborationGroupIfConnectedAsync(assignedSession.AgentId.Value, collaborationId);
            await Clients.Group(collaborationId).SendAsync("AgentAssigned", new
            {
                AgentId = assignedSession.AgentId,
                AgentName = assignedSession.Agent?.Name
            });
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "No agents available at the moment");
        }
    }

    public async Task RequestSupervisor(string collaborationId)
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        var userType = _connectionUserTypes.GetValueOrDefault(Context.ConnectionId);

        if (userType != "Agent" || !int.TryParse(userId, out var agentId))
        {
            await Clients.Caller.SendAsync("Error", "Only agents can request supervisors");
            return;
        }

        var session = await _collaborationService.GetSessionAsync(collaborationId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", "Collaboration session not found");
            return;
        }

        var availableSupervisors = await _collaborationService.GetAvailableSupervisorsAsync(session.ApplicationId, agentId);
        await Clients.Caller.SendAsync("AvailableSupervisors", availableSupervisors.Select(s => new
        {
            s.Id,
            s.Name,
            s.Department
        }));
    }

    public async Task AddSupervisor(string collaborationId, int supervisorId)
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        var userType = _connectionUserTypes.GetValueOrDefault(Context.ConnectionId);

        if (userType != "Agent" || !int.TryParse(userId, out var agentId))
        {
            await Clients.Caller.SendAsync("Error", "Only agents can add supervisors");
            return;
        }

        var session = await _collaborationService.GetSessionAsync(collaborationId);
        if (session == null)
        {
            await Clients.Caller.SendAsync("Error", "Collaboration session not found");
            return;
        }

        var assigned = await _supervisorAssignmentService.AssignSupervisorToSessionAsync(supervisorId, agentId, collaborationId);
        if (assigned)
        {
            var supervisor = await _dataService.GetSupervisorAsync(supervisorId);

            await AddSupervisorToCollaborationGroupIfConnectedAsync(supervisorId, collaborationId);

            await Clients.Group(collaborationId).SendAsync("SupervisorAdded", new
            {
                SupervisorId = supervisorId,
                SupervisorName = supervisor?.Name
            });
        }
        else
        {
            await Clients.Caller.SendAsync("Error", "Failed to assign supervisor");
        }
    }

    public async Task StartScreenShare(string collaborationId)
    {
        var streamUrl = await _streamingService.GenerateStreamUrlAsync(collaborationId);
        await Clients.Group(collaborationId).SendAsync("ScreenShareStarted", new
        {
            CollaborationId = collaborationId,
            StreamUrl = streamUrl
        });
    }

    public async Task StopScreenShare(string collaborationId)
    {
        await Clients.Group(collaborationId).SendAsync("ScreenShareStopped", new
        {
            CollaborationId = collaborationId
        });
    }

    public async Task SendWebRTCOffer(string collaborationId, object offer)
    {
        if (string.IsNullOrWhiteSpace(collaborationId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id");
            return;
        }

        await Clients.OthersInGroup(collaborationId).SendAsync("ReceiveWebRTCOffer", new
        {
            Offer = offer,
            FromConnectionId = Context.ConnectionId
        });
    }

    public async Task SendWebRTCOfferTo(string collaborationId, string targetConnectionId, object offer)
    {
        if (string.IsNullOrWhiteSpace(collaborationId) || string.IsNullOrWhiteSpace(targetConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id or target connection id");
            return;
        }

        await Clients.Client(targetConnectionId).SendAsync("ReceiveWebRTCOffer", new
        {
            Offer = offer,
            FromConnectionId = Context.ConnectionId
        });
    }

    public async Task SendWebRTCAnswer(string collaborationId, object answer)
    {
        if (string.IsNullOrWhiteSpace(collaborationId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id");
            return;
        }

        await Clients.OthersInGroup(collaborationId).SendAsync("ReceiveWebRTCAnswer", new
        {
            Answer = answer,
            FromConnectionId = Context.ConnectionId
        });
    }

    public async Task SendWebRTCAnswerTo(string collaborationId, string targetConnectionId, object answer)
    {
        if (string.IsNullOrWhiteSpace(collaborationId) || string.IsNullOrWhiteSpace(targetConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id or target connection id");
            return;
        }

        await Clients.Client(targetConnectionId).SendAsync("ReceiveWebRTCAnswer", new
        {
            Answer = answer,
            FromConnectionId = Context.ConnectionId
        });
    }

    public async Task SendWebRTCIceCandidate(string collaborationId, object candidate)
    {
        if (string.IsNullOrWhiteSpace(collaborationId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id");
            return;
        }

        await Clients.OthersInGroup(collaborationId).SendAsync("ReceiveWebRTCIceCandidate", new
        {
            Candidate = candidate,
            FromConnectionId = Context.ConnectionId
        });
    }

    public async Task SendWebRTCIceCandidateTo(string collaborationId, string targetConnectionId, object candidate)
    {
        if (string.IsNullOrWhiteSpace(collaborationId) || string.IsNullOrWhiteSpace(targetConnectionId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id or target connection id");
            return;
        }

        await Clients.Client(targetConnectionId).SendAsync("ReceiveWebRTCIceCandidate", new
        {
            Candidate = candidate,
            FromConnectionId = Context.ConnectionId
        });
    }

    public async Task ViewerReady(string collaborationId)
    {
        if (string.IsNullOrWhiteSpace(collaborationId))
        {
            await Clients.Caller.SendAsync("Error", "Invalid collaboration id");
            return;
        }

        await Clients.OthersInGroup(collaborationId).SendAsync("ViewerReady", new
        {
            CollaborationId = collaborationId,
            ViewerConnectionId = Context.ConnectionId
        });
    }

    public async Task SetTyping(string collaborationId, bool isTyping)
    {
        var userType = _connectionUserTypes.GetValueOrDefault(Context.ConnectionId);
        var userId = _connectionUserIds.GetValueOrDefault(Context.ConnectionId);

        if (string.IsNullOrWhiteSpace(collaborationId) || string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userType))
        {
            return;
        }

        await Clients.OthersInGroup(collaborationId).SendAsync("Typing", new
        {
            CollaborationId = collaborationId,
            UserId = userId,
            UserType = userType,
            IsTyping = isTyping
        });
    }

    public async Task StartRecording(string collaborationId)
    {
        var recordingUrl = await _streamingService.StartRecordingAsync(collaborationId);
        await Clients.Group(collaborationId).SendAsync("RecordingStarted", new
        {
            CollaborationId = collaborationId,
            RecordingUrl = recordingUrl
        });
    }

    public async Task<object?> GetMyAssignedSession()
    {
        var userId = Context.GetHttpContext()?.Request.Query["userId"].ToString();
        var userType = _connectionUserTypes.GetValueOrDefault(Context.ConnectionId);

        if (userType != "Agent" || !int.TryParse(userId, out var agentId))
        {
            return null;
        }

        var sessions = await _dataService.GetCollaborationSessionsAsync();
        var assigned = sessions
            .Where(s => s.AgentId == agentId && string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase) && s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();

        if (assigned == null || string.IsNullOrWhiteSpace(assigned.CollaborationId))
        {
            return null;
        }

        var agent = await _dataService.GetAgentAsync(agentId);
        return new
        {
            CollaborationId = assigned.CollaborationId,
            AgentId = agentId,
            AgentName = agent?.Name
        };
    }

    public async Task StopRecording(string collaborationId)
    {
        await _streamingService.StopRecordingAsync(collaborationId);
        var recordingUrl = await _streamingService.GetRecordingUrlAsync(collaborationId);
        await Clients.Group(collaborationId).SendAsync("RecordingStopped", new
        {
            CollaborationId = collaborationId,
            RecordingUrl = recordingUrl
        });
    }

    private static string NormalizeUserType(string? userType)
    {
        if (string.IsNullOrWhiteSpace(userType))
        {
            return "User";
        }

        userType = userType.Trim().ToLowerInvariant();
        return userType switch
        {
            "agent" => "Agent",
            "supervisor" => "Supervisor",
            _ => "User"
        };
    }
}
