namespace RaceResults.Web.Models;

public class ResultRecord
{
    public int Position { get; set; }
    public string Time { get; set; } = string.Empty;
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
