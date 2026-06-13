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

    /// <summary>Finishing status for this event (US16). Default <see cref="FinishStatus.Finished"/>; a non-finisher is treated as DNF unless set to DNS.</summary>
    public FinishStatus Status { get; set; } = FinishStatus.Finished;

    /// <summary>Reason recorded for a status change (required for DSQ) (US16 AC2/AC7).</summary>
    public string? StatusReason { get; set; }

    /// <summary>When the status was last changed (US16 AC7).</summary>
    public DateTime? StatusUpdatedAt { get; set; }

    public bool IsU18 => Age.HasValue && Age.Value < 18;
    public bool IsUnaffiliated => string.IsNullOrWhiteSpace(Club);
}
