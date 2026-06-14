namespace RaceResults.Web.Models;

public class RaceEvent
{
    public int Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public EventType EventType { get; set; }
    public bool IsCurrent { get; set; }

    /// <summary>When true the event is finalised and read-only (US20): mutations are rejected and it cannot be current.</summary>
    public bool IsArchived { get; set; }

    /// <summary>When true the event has a live public results page (US21).</summary>
    public bool IsPublished { get; set; }

    /// <summary>Unguessable token for the public results URL (US21); null until first published.</summary>
    public string? PublicToken { get; set; }
}
