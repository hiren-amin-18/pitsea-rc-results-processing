using System.ComponentModel.DataAnnotations;

namespace RaceResults.Web.Models;

public class CreateEventInput
{
    [Required]
    public string EventName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime EventDate { get; set; } = DateTime.Today;

    [Required]
    public EventType EventType { get; set; } = EventType.CrownToCrown;
}
