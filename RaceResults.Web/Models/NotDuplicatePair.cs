namespace RaceResults.Web.Models;

/// <summary>Records a user-confirmed "these two runners are NOT the same person" decision,
/// so the duplicates view stops surfacing the pair. Stored with the lower runner id first.</summary>
public class NotDuplicatePair
{
    public int Id { get; set; }
    public int RunnerAId { get; set; }
    public int RunnerBId { get; set; }
    public DateTime DismissedAt { get; set; }
}
