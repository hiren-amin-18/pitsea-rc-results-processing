using Microsoft.AspNetCore.Http;
using RaceResults.Web.Models;

namespace RaceResults.Web.Services;

public interface IRaceResultsService
{
    RaceEvent? GetCurrentEvent();
    IReadOnlyList<RaceEvent> GetEvents();
    OperationResult CreateEvent(CreateEventInput input);
    OperationResult UpdateEvent(EditEventInput input);
    OperationResult SetCurrentEvent(int eventId);
    OperationResult DeleteEvent(int eventId);

    /// <summary>Mark an event finalised/read-only (US20). Archiving the current event promotes another current event.</summary>
    OperationResult ArchiveEvent(int eventId);

    /// <summary>Restore normal editing for an archived event (US20).</summary>
    OperationResult UnarchiveEvent(int eventId);

    /// <summary>Publish an event's public results page, generating an unguessable token on first publish (US21).</summary>
    OperationResult PublishEvent(int eventId);

    /// <summary>Take an event's public results page offline (US21).</summary>
    OperationResult UnpublishEvent(int eventId);

    /// <summary>The event for a public token, or null when the token is unknown or the event is not published (US21).</summary>
    RaceEvent? GetPublishedEventByToken(string token);

    RaceStatusCounts GetStatusCounts();

    Task<OperationResult> UploadEntrantsAsync(IEnumerable<IFormFile> files);
    Task<OperationResult> UploadFinishBibAsync(IFormFile? file);
    Task<OperationResult> UploadTimingsAsync(IFormFile? file);

    IReadOnlyList<ResultRecord> GetCollatedResults();
    IReadOnlyList<ResultRecord> GetCollatedResults(int eventId);
    IReadOnlyList<Entrant> GetDnfEntrants();
    IReadOnlyList<Entrant> GetDnfEntrants(int eventId);

    /// <summary>Did Not Start entrants for the current event (US16).</summary>
    IReadOnlyList<Entrant> GetDnsEntrants();
    IReadOnlyList<Entrant> GetDnsEntrants(int eventId);

    /// <summary>Disqualified finishers for the current event, with their original position and reason (US16).</summary>
    IReadOnlyList<ResultRecord> GetDsqResults();
    IReadOnlyList<ResultRecord> GetDsqResults(int eventId);

    RaceStats GetRaceStats();
    RaceStats GetRaceStats(int eventId);

    /// <summary>Headline race statistics for the current event: completion, gender split, finish-time summary (US23).</summary>
    RaceStatisticsSummary GetRaceStatisticsSummary();
    RaceStatisticsSummary GetRaceStatisticsSummary(int eventId);

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
    byte[] GenerateResultsPdf(int eventId);

    /// <summary>Serialise the current event's collated results (and DNF entrants) to Excel-friendly CSV (US18).</summary>
    byte[] GenerateResultsCsv();
    byte[] GenerateResultsCsv(int eventId);

    /// <summary>Descriptive download filename for the results CSV, e.g. <c>crown-to-crown-2026-05-01-results.csv</c> (US18).</summary>
    string GetResultsCsvFileName();
    string GetResultsCsvFileName(int eventId);
}
