namespace RaceResults.Web.Models;

public class RaceEvent
{
    public int Id { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public EventType EventType { get; set; }
    public bool IsCurrent { get; set; }
}
