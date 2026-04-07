namespace CollaborationEngine.API.Models;

public class User
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? AdditionalDetails { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    
    public int ApplicationId { get; set; }
    public virtual Application Application { get; set; } = null!;
    
    // Navigation properties
    public virtual ICollection<CollaborationSession> CollaborationSessions { get; set; } = new List<CollaborationSession>();
    public virtual ICollection<ChatMessage> ChatMessages { get; set; } = new List<ChatMessage>();
}
