using CollaborationEngine.API.Models;
using CollaborationEngine.API.Services;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace CollaborationEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CollaborationController : ControllerBase
{
    private readonly ICollaborationService _collaborationService;
    private readonly IStreamingService _streamingService;
    private readonly ILocalDataService _dataService;

    public CollaborationController(
        ICollaborationService collaborationService,
        IStreamingService streamingService,
        ILocalDataService dataService)
    {
        _collaborationService = collaborationService;
        _streamingService = streamingService;
        _dataService = dataService;
    }

    [HttpPost("session/create")]
    public async Task<IActionResult> CreateSession([FromBody] CreateSessionRequest request)
    {
        var userId = request.UserId ?? 1;
        var applicationId = request.ApplicationId;

        if (!applicationId.HasValue)
        {
            if (!string.IsNullOrWhiteSpace(request.ApplicationName))
            {
                var apps = await _dataService.GetApplicationsAsync();
                var normalized = request.ApplicationName.Trim();
                var app = apps.FirstOrDefault(a => string.Equals(a.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
                if (app == null)
                {
                    app = await _dataService.SaveApplicationAsync(new Application
                    {
                        Name = normalized,
                        Description = "Auto created application",
                        ApiKey = string.Empty,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }
                applicationId = app.Id;
            }
            else
            {
                applicationId = 1;
            }
        }

        var appEntity = await _dataService.GetApplicationAsync(applicationId.Value);
        var session = await _collaborationService.CreateSessionAsync(userId, applicationId.Value);
        return Ok(new { 
            CollaborationId = session.CollaborationId,
            SessionId = session.Id,
            StartedAt = session.StartedAt,
            ApplicationId = session.ApplicationId,
            ApplicationName = appEntity?.Name
        });
    }

    [HttpGet("session/{collaborationId}")]
    public async Task<IActionResult> GetSession(string collaborationId)
    {
        var session = await _collaborationService.GetSessionAsync(collaborationId);
        if (session == null)
        {
            return NotFound("Collaboration session not found");
        }

        return Ok(new
        {
            session.Id,
            session.CollaborationId,
            session.Status,
            session.StartedAt,
            session.EndedAt,
            session.UserId,
            session.AgentId,
            AgentName = session.Agent?.Name,
            UserName = session.User?.Name
        });
    }

    [HttpGet("sessions/active")]
    public async Task<IActionResult> GetActiveSessions([FromQuery] string? appName = null)
    {
        var sessions = await _dataService.GetCollaborationSessionsAsync();
        var agents = await _dataService.GetAgentsAsync();
        var users = await _dataService.GetUsersAsync();
        var apps = await _dataService.GetApplicationsAsync();

        var agentsById = agents.ToDictionary(a => a.Id, a => a);
        var usersById = users.ToDictionary(u => u.Id, u => u);

        int? filterAppId = null;
        if (!string.IsNullOrWhiteSpace(appName))
        {
            var normalized = appName.Trim();
            var app = apps.FirstOrDefault(a => string.Equals(a.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (app != null)
            {
                filterAppId = app.Id;
            }
        }

        var appsById = apps.ToDictionary(a => a.Id, a => a);

        var active = sessions
            .Where(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase) && s.EndedAt == null)
            .Where(s => !filterAppId.HasValue || s.ApplicationId == filterAppId.Value)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.CollaborationId,
                s.StartedAt,
                s.UserId,
                s.AgentId,
                s.ApplicationId,
                ApplicationName = appsById.TryGetValue(s.ApplicationId, out var app) ? app.Name : null,
                UserName = usersById.TryGetValue(s.UserId, out var u) ? u.Name : null,
                AgentName = s.AgentId != null && agentsById.TryGetValue(s.AgentId.Value, out var a) ? a.Name : null
            })
            .ToList();

        return Ok(active);
    }

    [HttpPost("session/{collaborationId}/end")]
    public async Task<IActionResult> EndSession(string collaborationId)
    {
        await _collaborationService.EndSessionAsync(collaborationId);
        return Ok(new { Message = "Session ended successfully" });
    }

    [HttpGet("session/{collaborationId}/messages")]
    public async Task<IActionResult> GetChatHistory(string collaborationId)
    {
        var messages = await _collaborationService.GetChatHistoryAsync(collaborationId);
        return Ok(messages.Select(m => new
        {
            m.Id,
            m.Content,
            m.MessageType,
            m.SentAt,
            m.SenderType,
            m.SenderId
        }));
    }

    [HttpGet("transcript")]
    public async Task<IActionResult> GetTranscript([FromQuery] string? collaborationId = null, [FromQuery] string? userName = null)
    {
        CollaborationSession? session = null;

        if (!string.IsNullOrWhiteSpace(collaborationId))
        {
            session = await _collaborationService.GetSessionAsync(collaborationId);
        }
        else if (!string.IsNullOrWhiteSpace(userName))
        {
            var users = await _dataService.GetUsersAsync();
            var normalized = userName.Trim();
            var user = users.FirstOrDefault(u => string.Equals(u.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
            if (user == null)
            {
                return NotFound($"User not found (name={userName})");
            }

            var sessions = await _dataService.GetCollaborationSessionsAsync();
            var candidate = sessions
                .Where(s => s.UserId == user.Id)
                .OrderByDescending(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase) && s.EndedAt == null)
                .ThenByDescending(s => s.StartedAt)
                .FirstOrDefault();

            if (candidate == null || string.IsNullOrWhiteSpace(candidate.CollaborationId))
            {
                return NotFound($"No collaboration session found for user (name={userName}, userId={user.Id})");
            }

            session = await _collaborationService.GetSessionAsync(candidate.CollaborationId);
        }
        else
        {
            return BadRequest("Either collaborationId or userName query parameter must be provided");
        }

        if (session == null)
        {
            return NotFound("Collaboration session not found");
        }

        var app = await _dataService.GetApplicationAsync(session.ApplicationId);

        var recordingId = !string.IsNullOrWhiteSpace(session.RecordingUrl)
            ? session.RecordingUrl.Trim().Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault()
            : null;

        var supervisors = await _dataService.GetSupervisorsAsync();
        var supervisorsById = supervisors.ToDictionary(s => s.Id, s => s);
        var supervisorSessions = (await _dataService.GetAgentSupervisorSessionsAsync())
            .Where(s => s.CollaborationSessionId == session.Id)
            .OrderBy(s => s.JoinedAt)
            .ToList();

        var transcriptMessages = (await _dataService.GetChatMessagesAsync(session.Id))
            .OrderBy(m => m.SentAt)
            .ToList();

        var senderUsers = new System.Collections.Generic.Dictionary<int, User>();
        var senderAgents = new System.Collections.Generic.Dictionary<int, Agent>();

        foreach (var msg in transcriptMessages)
        {
            if (!msg.SenderId.HasValue)
            {
                continue;
            }

            if (string.Equals(msg.SenderType, "User", StringComparison.OrdinalIgnoreCase))
            {
                if (!senderUsers.ContainsKey(msg.SenderId.Value))
                {
                    var u = await _dataService.GetUserAsync(msg.SenderId.Value);
                    if (u != null)
                    {
                        senderUsers[msg.SenderId.Value] = u;
                    }
                }
            }
            else if (string.Equals(msg.SenderType, "Agent", StringComparison.OrdinalIgnoreCase))
            {
                if (!senderAgents.ContainsKey(msg.SenderId.Value))
                {
                    var a = await _dataService.GetAgentAsync(msg.SenderId.Value);
                    if (a != null)
                    {
                        senderAgents[msg.SenderId.Value] = a;
                    }
                }
            }
            else if (string.Equals(msg.SenderType, "Supervisor", StringComparison.OrdinalIgnoreCase))
            {
                if (!supervisorsById.ContainsKey(msg.SenderId.Value))
                {
                    var s = await _dataService.GetSupervisorAsync(msg.SenderId.Value);
                    if (s != null)
                    {
                        supervisorsById[msg.SenderId.Value] = s;
                    }
                }
            }
        }

        return Ok(new
        {
            Session = new
            {
                session.Id,
                session.CollaborationId,
                session.Status,
                session.StartedAt,
                session.EndedAt,
                session.UpdatedAt,
                session.ApplicationId,
                ApplicationName = app?.Name,
                session.UserId,
                session.AgentId,
                RecordingId = recordingId,
                RecordingUrl = session.RecordingUrl,
                session.IsRecording
            },
            User = session.User == null ? null : new
            {
                session.User.Id,
                session.User.Name,
                session.User.Email,
                session.User.PhoneNumber,
                session.User.ApplicationId
            },
            Agent = session.Agent == null ? null : new
            {
                session.Agent.Id,
                session.Agent.Name,
                session.Agent.Email,
                session.Agent.PhoneNumber,
                session.Agent.ApplicationId
            },
            Supervisors = supervisorSessions.Select(ss => new
            {
                ss.Id,
                ss.SupervisorId,
                SupervisorName = supervisorsById.TryGetValue(ss.SupervisorId, out var sup) ? sup.Name : null,
                ss.AgentId,
                ss.JoinedAt,
                ss.LeftAt,
                ss.IsActive
            }),
            Transcript = transcriptMessages.Select(m => new
            {
                m.Id,
                m.Content,
                m.MessageType,
                m.SentAt,
                m.SenderType,
                m.SenderId,
                SenderName = m.SenderId.HasValue
                    ? (string.Equals(m.SenderType, "User", StringComparison.OrdinalIgnoreCase)
                        ? (senderUsers.TryGetValue(m.SenderId.Value, out var u) ? u.Name : session.User?.Name)
                        : string.Equals(m.SenderType, "Agent", StringComparison.OrdinalIgnoreCase)
                            ? (senderAgents.TryGetValue(m.SenderId.Value, out var a) ? a.Name : session.Agent?.Name)
                            : string.Equals(m.SenderType, "Supervisor", StringComparison.OrdinalIgnoreCase)
                                ? (supervisorsById.TryGetValue(m.SenderId.Value, out var s) ? s.Name : null)
                                : null)
                    : null
            })
        });
    }

    [HttpPost("session/{collaborationId}/message")]
    public async Task<IActionResult> AddMessage(string collaborationId, [FromBody] AddMessageRequest request)
    {
        // Demo mode: treat as user message
        var message = await _collaborationService.AddMessageAsync(collaborationId, request.Content, "User", 1);

        return Ok(new
        {
            message.Id,
            message.Content,
            message.MessageType,
            message.SentAt,
            message.SenderType,
            message.SenderId
        });
    }

    [HttpPost("session/{collaborationId}/recording/start")]
    public async Task<IActionResult> StartRecording(string collaborationId)
    {
        var recordingUrl = await _streamingService.StartRecordingAsync(collaborationId);
        return Ok(new { RecordingUrl = recordingUrl });
    }

    [HttpPost("session/{collaborationId}/recording/stop")]
    public async Task<IActionResult> StopRecording(string collaborationId)
    {
        await _streamingService.StopRecordingAsync(collaborationId);
        var recordingUrl = await _streamingService.GetRecordingUrlAsync(collaborationId);
        return Ok(new { RecordingUrl = recordingUrl });
    }

    [HttpGet("session/{collaborationId}/recording/status")]
    public async Task<IActionResult> GetRecordingStatus(string collaborationId)
    {
        var isRecording = await _streamingService.IsRecordingAsync(collaborationId);
        var recordingUrl = await _streamingService.GetRecordingUrlAsync(collaborationId);
        return Ok(new { IsRecording = isRecording, RecordingUrl = recordingUrl });
    }

    [HttpGet("session/{collaborationId}/stream/url")]
    public async Task<IActionResult> GetStreamUrl(string collaborationId)
    {
        var streamUrl = await _streamingService.GenerateStreamUrlAsync(collaborationId);
        return Ok(new { StreamUrl = streamUrl });
    }
}

public record CreateSessionRequest(int? UserId = null, int? ApplicationId = null, string? ApplicationName = null);
public record AddMessageRequest(string Content);
