using Microsoft.AspNetCore.Http;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public interface IRaceResultsService
{
    RaceEvent GetCurrentEvent();
    IReadOnlyList<RaceEvent> GetEvents();
    OperationResult CreateEvent(CreateEventInput input);
    OperationResult UpdateEvent(EditEventInput input);
    OperationResult SetCurrentEvent(int eventId);
    OperationResult DeleteEvent(int eventId);

    RaceStatusCounts GetStatusCounts();

    Task<OperationResult> UploadEntrantsAsync(IEnumerable<IFormFile> files);
    Task<OperationResult> UploadFinishBibAsync(IFormFile? file);
    Task<OperationResult> UploadTimingsAsync(IFormFile? file);

    IReadOnlyList<ResultRecord> GetCollatedResults();
    IReadOnlyList<ResultRecord> GetCollatedResults(int eventId);
    IReadOnlyList<Entrant> GetDnfEntrants();
    RaceStats GetRaceStats();
    IReadOnlyList<TopTenCategory> GetTopTenByCategory();
    IReadOnlyList<TopTenCategory> GetTopTenByCategory(int eventId);

    bool TryGetEditableResult(int position, out EditResultInput editInput);
    OperationResult UpdateResult(EditResultInput editInput);
    byte[] GenerateResultsPdf();

    /// <summary>Serialise the current event's collated results (and DNF entrants) to Excel-friendly CSV (US18).</summary>
    byte[] GenerateResultsCsv();

    /// <summary>Descriptive download filename for the results CSV, e.g. <c>crown-to-crown-2026-05-01-results.csv</c> (US18).</summary>
    string GetResultsCsvFileName();
}
