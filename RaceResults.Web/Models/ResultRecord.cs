namespace RaceResults.Web.Models;

public class ResultRecord
{
    public int Position { get; set; }
    public string Time { get; set; } = string.Empty;
    public string BibNumber { get; set; } = string.Empty;
    public Entrant? Entrant { get; set; }

    public string Name => Entrant?.Name ?? "Unmatched bib";
    public string Club => string.IsNullOrWhiteSpace(Entrant?.Club) ? "Unaffiliated" : Entrant!.Club;
    public string Gender => Entrant?.Gender ?? "Unknown";
    public int? Age => Entrant?.Age;
}
