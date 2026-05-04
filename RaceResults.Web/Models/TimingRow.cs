namespace RaceResults.Web.Models;

public class TimingRow
{
    public int Id { get; set; }
    public int EventId { get; set; } = 1;
    public int Position { get; set; }
    public string Time { get; set; } = string.Empty;
}
