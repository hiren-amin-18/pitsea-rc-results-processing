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

    /// <summary>Links this per-event entry to a persistent <see cref="Runner"/> (US15). Nullable for legacy rows pre-migration.</summary>
    public int? RunnerId { get; set; }
    public Runner? Runner { get; set; }

    public bool IsU18 => Age.HasValue && Age.Value < 18;
    public bool IsUnaffiliated => string.IsNullOrWhiteSpace(Club);
}
