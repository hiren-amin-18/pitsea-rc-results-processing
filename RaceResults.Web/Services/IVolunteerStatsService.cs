using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

/// <summary>Per-event and season volunteer statistics (US29). Pure aggregation over US28's assignments.</summary>
public interface IVolunteerStatsService
{
    EventVolunteerStats GetEventStats(int eventId);
    SeasonVolunteerStats GetSeasonStats(int year);
    IReadOnlyList<int> GetAvailableYears();
}
