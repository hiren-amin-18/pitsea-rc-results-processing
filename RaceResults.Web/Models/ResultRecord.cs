namespace RaceResults.Web.Models;

public class ResultRecord
{
    public int Position { get; set; }

    /// <summary>Closed-up rank for display after any DSQ removals (US16); the stored finish position is unchanged in <see cref="Position"/>.</summary>
    public int DisplayPosition { get; set; }

    /// <summary>Finishing status (US16).</summary>
    public FinishStatus Status { get; set; } = FinishStatus.Finished;

    /// <summary>Reason recorded for a DSQ (US16).</summary>
    public string? StatusReason { get; set; }

    /// <summary>Canonical display time (or original raw text for unparseable legacy rows). For display only.</summary>
    public string Time { get; set; } = string.Empty;

    /// <summary>Typed finish duration; null when no timing row or an unparseable legacy value. Use this for comparison/sorting (US17).</summary>
    public TimeSpan? Duration { get; set; }

    public string BibNumber { get; set; } = string.Empty;
    public Entrant? Entrant { get; set; }

    /// <summary>True when this finish bib has no matching registered entrant (US04).</summary>
    public bool IsUnmatched => Entrant is null;

    public string Name => Entrant?.Name ?? "Unmatched bib";
    public string Club => Entrant?.Club ?? string.Empty;
    public string Gender => Entrant?.Gender ?? "Unknown";
    public int? Age => Entrant?.Age;
    public bool IsU18 => Entrant?.IsU18 ?? false;
}
