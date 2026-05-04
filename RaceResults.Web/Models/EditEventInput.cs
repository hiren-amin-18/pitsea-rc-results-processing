using System.ComponentModel.DataAnnotations;

namespace RaceResults.Web.Models;

public class EditEventInput
{
    [Required]
    public int Id { get; set; }

    [Required]
    public string EventName { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    public DateTime EventDate { get; set; }

    [Required]
    public EventType EventType { get; set; }
}
