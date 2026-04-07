namespace CollaborationEngine.API.Models;

public class Supervisor
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Department { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public DateTime LastActiveAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public int ApplicationId { get; set; }
    public virtual Application Application { get; set; } = null!;
    
    // Navigation properties
    public virtual ICollection<AgentSupervisorSession> AgentSessions { get; set; } = new List<AgentSupervisorSession>();
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
