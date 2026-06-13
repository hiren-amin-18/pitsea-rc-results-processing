namespace RaceResults.Web.Models;

/// <summary>A runner plus their cross-event race count, for the runner management list (US15).</summary>
public class RunnerListItem
{
    public required Runner Runner { get; init; }
    public int RaceCount { get; init; }
}
