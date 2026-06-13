namespace RaceResults.Web.Models;

/// <summary>
/// A persistent person who races across events (US15). Per-event <see cref="Entrant"/> rows link to a
/// runner so results aggregate reliably instead of being matched by name+club each time.
/// </summary>
public class Runner
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Club { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;

    /// <summary>Age, recorded only for under-18s by club convention (a blank age means adult).</summary>
    public int? Age { get; set; }

    /// <summary>Optional external reference such as an England Athletics number.</summary>
    public string? ExternalReference { get; set; }

    /// <summary>False once a runner has no remaining entrants (e.g. after their only event was deleted) — kept, not removed (US15 AC7).</summary>
    public bool IsActive { get; set; } = true;

    public bool IsU18 => Age.HasValue && Age.Value < 18;
    public bool IsUnaffiliated => string.IsNullOrWhiteSpace(Club);
}
