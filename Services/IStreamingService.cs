namespace CollaborationEngine.API.Services;

public interface IStreamingService
{
    Task<string> StartRecordingAsync(string collaborationId);
    Task StopRecordingAsync(string collaborationId);
    Task<string> GetRecordingUrlAsync(string collaborationId);
    Task<bool> IsRecordingAsync(string collaborationId);
    Task<string> GenerateStreamUrlAsync(string collaborationId);
}
