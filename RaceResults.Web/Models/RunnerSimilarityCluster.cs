namespace RaceResults.Web.Models;

public enum SimilarityReason
{
    SameNameDifferentClub,
    FuzzyNameMatch,
}

public class RunnerSimilarityCluster
{
    public required IReadOnlyList<RunnerListItem> Runners { get; init; }
    public required SimilarityReason Reason { get; init; }
}
