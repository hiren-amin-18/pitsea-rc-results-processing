namespace RaceResults.Web.Models;

public class EventsPageViewModel
{
    public List<RaceEvent> Events { get; set; } = new();
    public int? CurrentEventId { get; set; }
}
