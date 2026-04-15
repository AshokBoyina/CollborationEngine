using CollaborationEngine.API.Services;
using Microsoft.AspNetCore.Mvc;

namespace CollaborationEngine.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RecordingController : ControllerBase
{
    private readonly ILocalDataService _dataService;
    private readonly IStreamingService _streamingService;
    private readonly IWebHostEnvironment _environment;

    private string WebRootOrFallback =>
        _environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot");

    private sealed record RecordingInfo(
        string FileName,
        string CollaborationId,
        long Size,
        DateTime Created,
        string Url,
        string? AgentName,
        string? UserName);

    public RecordingController(
        ILocalDataService dataService,
        IStreamingService streamingService,
        IWebHostEnvironment environment)
    {
        _dataService = dataService;
        _streamingService = streamingService;
        _environment = environment;
    }

    [HttpPost("session/{collaborationId}/recording/upload")]
    public async Task<IActionResult> UploadRecording(string collaborationId, IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No file uploaded");
            }

            // Create recordings directory if it doesn't exist
            Directory.CreateDirectory(WebRootOrFallback);
            var recordingsPath = Path.Combine(WebRootOrFallback, "recordings", collaborationId);
            Directory.CreateDirectory(recordingsPath);

            // Generate unique filename
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var fileName = $"{collaborationId}_{timestamp}.webm";
            var filePath = Path.Combine(recordingsPath, fileName);

            // Save file
            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Update session with recording URL
            var session = await _dataService.GetCollaborationSessionAsync(collaborationId);
            if (session != null)
            {
                session.RecordingUrl = $"/recordings/{collaborationId}/{fileName}";
                await _dataService.SaveCollaborationSessionAsync(session);
            }

            var recordingUrl = $"{Request.Scheme}://{Request.Host}/recordings/{collaborationId}/{fileName}";
            
            return Ok(new { 
                recordingUrl = recordingUrl,
                fileName = fileName,
                size = file.Length,
                contentType = file.ContentType
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error uploading recording: {ex.Message}");
        }
    }

    [HttpGet("session/{collaborationId}/recordings")]
    public async Task<IActionResult> GetRecordings(string collaborationId)
    {
        try
        {
            var sessions = await _dataService.GetCollaborationSessionsAsync();
            var agents = await _dataService.GetAgentsAsync();
            var users = await _dataService.GetUsersAsync();

            var sessionsByCollaborationId = sessions
                .Where(s => !string.IsNullOrWhiteSpace(s.CollaborationId))
                .GroupBy(s => s.CollaborationId.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.StartedAt).First(), StringComparer.OrdinalIgnoreCase);

            var agentsById = agents.ToDictionary(a => a.Id, a => a);
            var usersById = users.ToDictionary(u => u.Id, u => u);

            List<RecordingInfo> recordings;

            if (collaborationId.ToLower() == "all")
            {
                // Get all recordings from all sessions
                Directory.CreateDirectory(WebRootOrFallback);
                var recordingsPath = Path.Combine(WebRootOrFallback, "recordings");
                
                if (!Directory.Exists(recordingsPath))
                {
                    return Ok(new List<RecordingInfo>());
                }

                recordings = new List<RecordingInfo>();
                
                // Get all session directories
                var sessionDirectories = Directory.GetDirectories(recordingsPath);
                
                foreach (var sessionDir in sessionDirectories)
                {
                    var sessionId = Path.GetFileName(sessionDir);
                    var files = Directory.GetFiles(sessionDir, "*.webm");

                    sessionsByCollaborationId.TryGetValue(sessionId, out var session);
                    var agentName = (session?.AgentId != null && agentsById.TryGetValue(session.AgentId.Value, out var a)) ? a.Name : null;
                    var userName = (session != null && usersById.TryGetValue(session.UserId, out var u)) ? u.Name : null;
                    
                    foreach (var file in files)
                    {
                        recordings.Add(new RecordingInfo(
                            FileName: Path.GetFileName(file),
                            CollaborationId: sessionId,
                            Size: new FileInfo(file).Length,
                            Created: new FileInfo(file).CreationTime,
                            Url: $"{Request.Scheme}://{Request.Host}/recordings/{sessionId}/{Path.GetFileName(file)}",
                            AgentName: agentName,
                            UserName: userName));
                    }
                }
            }
            else
            {
                // Get recordings for specific session
                Directory.CreateDirectory(WebRootOrFallback);
                var recordingsPath = Path.Combine(WebRootOrFallback, "recordings", collaborationId);
                
                if (!Directory.Exists(recordingsPath))
                {
                    return Ok(new List<RecordingInfo>());
                }

                sessionsByCollaborationId.TryGetValue(collaborationId.Trim(), out var session);
                var agentName = (session?.AgentId != null && agentsById.TryGetValue(session.AgentId.Value, out var a)) ? a.Name : null;
                var userName = (session != null && usersById.TryGetValue(session.UserId, out var u)) ? u.Name : null;

                recordings = Directory.GetFiles(recordingsPath, "*.webm")
                    .Select(file => new RecordingInfo(
                        FileName: Path.GetFileName(file),
                        CollaborationId: collaborationId,
                        Size: new FileInfo(file).Length,
                        Created: new FileInfo(file).CreationTime,
                        Url: $"{Request.Scheme}://{Request.Host}/recordings/{collaborationId}/{Path.GetFileName(file)}",
                        AgentName: agentName,
                        UserName: userName))
                    .ToList();
            }

            return Ok(recordings.OrderByDescending(r => r.Created).ToList());
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error getting recordings: {ex.Message}");
        }
    }

    [HttpGet("recordings/{collaborationId}/{fileName}")]
    public async Task<IActionResult> GetRecording(string collaborationId, string fileName)
    {
        try
        {
            Directory.CreateDirectory(WebRootOrFallback);
            var filePath = Path.Combine(WebRootOrFallback, "recordings", collaborationId, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound("Recording not found");
            }

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            return File(fileBytes, "video/webm", fileName);
        }
        catch (Exception ex)
        {
            return StatusCode(500, $"Error getting recording: {ex.Message}");
        }
    }
}
