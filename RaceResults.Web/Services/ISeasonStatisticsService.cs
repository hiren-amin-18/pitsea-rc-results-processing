using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Cross-event season statistics and per-runner season profiles (US24).</summary>
public interface ISeasonStatisticsService
{
    /// <summary>Calendar years that have at least one event, descending.</summary>
    IReadOnlyList<int> GetAvailableSeasons();

    /// <summary>Season-wide dashboard for a calendar year, across all event types.</summary>
    SeasonDashboard GetSeasonDashboard(int year);

    /// <summary>One runner's season profile for a calendar year. Null if the runner does not exist.</summary>
    RunnerSeasonProfile? GetRunnerSeasonProfile(int runnerId, int year);
}
