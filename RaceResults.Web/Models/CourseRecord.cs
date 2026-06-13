namespace RaceResults.Web.Models;

/// <summary>
/// A course record for an event type and category (US22). Superseded records are retained
/// (<see cref="IsCurrent"/> = false) so record history stays queryable.
/// </summary>
public class CourseRecord
{
    public int Id { get; set; }
    public EventType EventType { get; set; }

    /// <summary>One of the four category names: Male, Female, Male U18, Female U18.</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>Record time as ticks — typed for reliable comparison (US22 AC7).</summary>
    public long DurationTicks { get; set; }

    public string RunnerName { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;

    /// <summary>Name of the race where the record was set (descriptive).</summary>
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }

    /// <summary>The in-app event that set this record, when applicable (null for seeded historical records).</summary>
    public int? SourceEventId { get; set; }

    /// <summary>True for the standing record; false for superseded history.</summary>
    public bool IsCurrent { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public TimeSpan Duration => TimeSpan.FromTicks(DurationTicks);
}
