namespace RaceResults.Web.Models;

public class Entrant
{
    public int Id { get; set; }
    public int EventId { get; set; } = 1;
    public string BibNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public int? Age { get; set; }

    public bool IsU18 => Age.HasValue && Age.Value < 18;
    public bool IsUnaffiliated => string.IsNullOrWhiteSpace(Club);
}
