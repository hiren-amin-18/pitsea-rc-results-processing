namespace RaceResults.Web.Models;

public class MergeBatchInput
{
    public List<ClusterMergeInput> Clusters { get; set; } = new();
}

public class ClusterMergeInput
{
    public int TargetId { get; set; }
    public List<int> SourceIds { get; set; } = new();
}
