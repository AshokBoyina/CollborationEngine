namespace CollaborationEngine.API.Models;

public class AgentSupervisorSession
{
    public int Id { get; set; }
    public DateTime JoinedAt { get; set; }
    public DateTime? LeftAt { get; set; }
    public bool IsActive { get; set; } = true;
    
    // Foreign keys
    public int CollaborationSessionId { get; set; }
    public int AgentId { get; set; }
    public int SupervisorId { get; set; }
    
    // Navigation properties
    public virtual CollaborationSession CollaborationSession { get; set; } = null!;
    public virtual Agent Agent { get; set; } = null!;
    public virtual Supervisor Supervisor { get; set; } = null!;
}
