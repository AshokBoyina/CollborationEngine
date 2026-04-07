namespace CollaborationEngine.API.Models;

public class ChatMessage
{
    public int Id { get; set; }
    public string Content { get; set; } = string.Empty;
    public string MessageType { get; set; } = "Text"; // Text, System, Bot
    public DateTime SentAt { get; set; }
    public bool IsRead { get; set; } = false;
    
    // Foreign keys
    public int CollaborationSessionId { get; set; }
    public int? SenderId { get; set; }
    public string SenderType { get; set; } = string.Empty; // User, Agent, Supervisor, Bot
    
    // Navigation properties
    public virtual CollaborationSession CollaborationSession { get; set; } = null!;
    public virtual User? User { get; set; }
    public virtual Agent? Agent { get; set; }
    public virtual Supervisor? Supervisor { get; set; }
}
