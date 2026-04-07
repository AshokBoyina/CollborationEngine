using CollaborationEngine.API.Models;
using CollaborationEngine.API.Services;
using Microsoft.AspNetCore.Mvc;

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
        var applicationId = request.ApplicationId ?? 1;
        var session = await _collaborationService.CreateSessionAsync(userId, applicationId);
        return Ok(new { 
            CollaborationId = session.CollaborationId,
            SessionId = session.Id,
            StartedAt = session.StartedAt
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
    public async Task<IActionResult> GetActiveSessions()
    {
        var sessions = await _dataService.GetCollaborationSessionsAsync();
        var active = sessions
            .Where(s => string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase) && s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .Select(s => new
            {
                s.Id,
                s.CollaborationId,
                s.StartedAt,
                s.UserId,
                s.AgentId
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

public record CreateSessionRequest(int? UserId = null, int? ApplicationId = null);
public record AddMessageRequest(string Content);
