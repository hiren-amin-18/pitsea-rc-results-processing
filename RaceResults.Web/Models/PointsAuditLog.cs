namespace RaceResults.Web.Models;

public class PointsAuditLog
{
    public int Id { get; set; }
    
    /// <summary>Season year for which points were awarded</summary>
    public int SeasonYear { get; set; }
    
    /// <summary>The event during which points were scored</summary>
    public int EventId { get; set; }
    public RaceEvent? Event { get; set; }
    
    /// <summary>The runner who earned points</summary>
    public int EntrantId { get; set; }
    public Entrant? Entrant { get; set; }
    
    /// <summary>The category (Male, Female, Male U18, Female U18)</summary>
    public string Category { get; set; } = string.Empty;
    
    /// <summary>Points awarded in this event</summary>
    public int PointsAwarded { get; set; }
    
    /// <summary>Type of audit entry: Initial, Recalculated, or Voided</summary>
    public AuditAction Action { get; set; }
    
    /// <summary>When this scoring action occurred</summary>
    public DateTime AuditTimestamp { get; set; }
    
    /// <summary>Optional reason for the action (e.g., "Result edit: position changed from 5 to 3")</summary>
    public string? Reason { get; set; }
}

public enum AuditAction
{
    /// <summary>Initial points awarded after event completion</summary>
    Initial = 0,
    
    /// <summary>Points recalculated due to result edit</summary>
    Recalculated = 1,
    
    /// <summary>Points voided (e.g., event cancelled)</summary>
    Voided = 2
}
