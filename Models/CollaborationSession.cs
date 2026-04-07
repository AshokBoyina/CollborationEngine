namespace CollaborationEngine.API.Models;

public class CollaborationSession
{
    public int Id { get; set; }
    public string CollaborationId { get; set; } = string.Empty; // Unique identifier for the collaboration
    public string Status { get; set; } = "Active"; // Active, Ended, Paused
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? EndedAt { get; set; }
    public string? RecordingUrl { get; set; }
    public bool IsRecording { get; set; } = false;
    public string? StreamUrl { get; set; }
    
    // Foreign keys
    public int UserId { get; set; }
    public int? AgentId { get; set; }
    public int ApplicationId { get; set; }
    
    // Navigation properties
    public virtual User User { get; set; } = null!;
    public virtual Agent? Agent { get; set; }
    public virtual Application Application { get; set; } = null!;
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
    public virtual ICollection<AgentSupervisorSession> SupervisorSessions { get; set; } = new List<AgentSupervisorSession>();
}
