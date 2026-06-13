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

    /// <summary>Did Not Start entrants for the current event (US16).</summary>
    IReadOnlyList<Entrant> GetDnsEntrants();

    /// <summary>Disqualified finishers for the current event, with their original position and reason (US16).</summary>
    IReadOnlyList<ResultRecord> GetDsqResults();

    RaceStats GetRaceStats();
    IReadOnlyList<TopTenCategory> GetTopTenByCategory();
    IReadOnlyList<TopTenCategory> GetTopTenByCategory(int eventId);

    bool TryGetEditableResult(int position, out EditResultInput editInput);
    OperationResult UpdateResult(EditResultInput editInput);

    /// <summary>Disqualify a finisher (by stored position) with a required reason (US16).</summary>
    OperationResult DisqualifyResult(int position, string reason);

    /// <summary>Reverse a disqualification, restoring the finisher to the results (US16).</summary>
    OperationResult ReinstateResult(int position);

    /// <summary>Set a non-finisher's status to DNS or DNF (US16).</summary>
    OperationResult SetNonFinisherStatus(string bibNumber, FinishStatus status);
    byte[] GenerateResultsPdf();

    /// <summary>Serialise the current event's collated results (and DNF entrants) to Excel-friendly CSV (US18).</summary>
    byte[] GenerateResultsCsv();

    /// <summary>Descriptive download filename for the results CSV, e.g. <c>crown-to-crown-2026-05-01-results.csv</c> (US18).</summary>
    string GetResultsCsvFileName();
}
