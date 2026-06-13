using System.ComponentModel.DataAnnotations.Schema;

namespace RaceResults.Web.Models;

public class TimingRow
{
    public int Id { get; set; }
    public int EventId { get; set; } = 1;
    public int Position { get; set; }

    /// <summary>Original uploaded text, kept for audit (US17 AC3).</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>Canonical, sortable duration in ticks. Null only for legacy rows whose text could not be parsed (US17).</summary>
    public long? DurationTicks { get; set; }

    [NotMapped]
    public TimeSpan? Duration => DurationTicks.HasValue ? TimeSpan.FromTicks(DurationTicks.Value) : null;
}
