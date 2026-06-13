using System.ComponentModel.DataAnnotations;

namespace RaceResults.Web.Models;

/// <summary>One event-type/category slot for the management list (US22), with its current record if any.</summary>
public class CourseRecordSlot
{
    public required EventType EventType { get; init; }
    public required string Category { get; init; }
    public CourseRecord? Current { get; init; }
}

/// <summary>Form input to set or correct a course record (US22).</summary>
public class EditCourseRecordInput
{
    [Required]
    public EventType EventType { get; set; }

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public string Time { get; set; } = string.Empty;

    [Required]
    public string RunnerName { get; set; } = string.Empty;

    public string? Club { get; set; }

    [Required]
    public string EventName { get; set; } = string.Empty;

    [DataType(DataType.Date)]
    public DateTime EventDate { get; set; } = DateTime.Today;
}

/// <summary>A category winner whose time beats (or first-sets) the stored course record, awaiting confirmation (US22 AC4).</summary>
public class PendingCourseRecord
{
    public required string Category { get; init; }
    public required string Time { get; init; }
    public required string RunnerName { get; init; }
    public string? Club { get; init; }

    /// <summary>The current record time being beaten, or null when this would be the first record.</summary>
    public string? PreviousTime { get; init; }
}
