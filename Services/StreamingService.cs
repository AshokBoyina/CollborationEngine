namespace CollaborationEngine.API.Services;

public class StreamingService : IStreamingService
{
    private readonly ILocalDataService _dataService;
    private static readonly Dictionary<string, bool> _recordingSessions = new();
    private static readonly Dictionary<string, string> _streamUrls = new();
    private static readonly object _lock = new object();

    public StreamingService(ILocalDataService dataService)
    {
        _dataService = dataService;
    }

    public async Task<string> StartRecordingAsync(string collaborationId)
    {
        lock (_lock)
        {
            if (_recordingSessions.ContainsKey(collaborationId) && _recordingSessions[collaborationId])
            {
                return _streamUrls.GetValueOrDefault(collaborationId, "");
            }

            _recordingSessions[collaborationId] = true;
            var recordingUrl = GenerateRecordingUrl(collaborationId);
            _streamUrls[collaborationId] = recordingUrl;

            // Update session in local store
            var session = _dataService.GetCollaborationSessionAsync(collaborationId).Result;
            if (session != null)
            {
                session.IsRecording = true;
                session.RecordingUrl = recordingUrl;
                session.UpdatedAt = DateTime.UtcNow;
                _dataService.SaveCollaborationSessionAsync(session).Wait();
            }

            return recordingUrl;
        }
    }

    public async Task StopRecordingAsync(string collaborationId)
    {
        lock (_lock)
        {
            if (_recordingSessions.ContainsKey(collaborationId))
            {
                _recordingSessions[collaborationId] = false;

                // Update session in local store
                var session = _dataService.GetCollaborationSessionAsync(collaborationId).Result;
                if (session != null)
                {
                    session.IsRecording = false;
                    session.UpdatedAt = DateTime.UtcNow;
                    _dataService.SaveCollaborationSessionAsync(session).Wait();
                }
            }
        }
    }

    public async Task<string> GetRecordingUrlAsync(string collaborationId)
    {
        lock (_lock)
        {
            return _streamUrls.GetValueOrDefault(collaborationId, "");
        }
    }

    public async Task<bool> IsRecordingAsync(string collaborationId)
    {
        lock (_lock)
        {
            return _recordingSessions.GetValueOrDefault(collaborationId, false);
        }
    }

    public async Task<string> GenerateStreamUrlAsync(string collaborationId)
    {
        lock (_lock)
        {
            var streamUrl = $"wss://localhost:7080/stream/{collaborationId}";
            _streamUrls[collaborationId] = streamUrl;

            // Update session in local store
            var session = _dataService.GetCollaborationSessionAsync(collaborationId).Result;
            if (session != null)
            {
                session.StreamUrl = streamUrl;
                session.UpdatedAt = DateTime.UtcNow;
                _dataService.SaveCollaborationSessionAsync(session).Wait();
            }

            return streamUrl;
        }
    }

    private string GenerateRecordingUrl(string collaborationId)
    {
        // Local storage simulation (actual upload handled by RecordingController)
        return $"https://localhost:7080/recordings/{collaborationId}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.webm";
    }
}
