namespace RaceResults.Web.Models;

public class FinishBibRecord
{
    public int Id { get; set; }
    public int EventId { get; set; } = 1;
    public int Position { get; set; }
    public string BibNumber { get; set; } = string.Empty;
}
