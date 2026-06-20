namespace RaceResults.Web.Models;

public class RunnersIndexViewModel
{
    public required IReadOnlyList<RunnerListItem> Runners { get; init; }
    public bool ShowDuplicates { get; init; }
    public IReadOnlyList<RunnerSimilarityCluster> Clusters { get; init; } = Array.Empty<RunnerSimilarityCluster>();
    public int DismissedPairCount { get; init; }
}
