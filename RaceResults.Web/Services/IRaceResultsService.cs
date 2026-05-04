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
    IReadOnlyList<Entrant> GetDnfEntrants();
    RaceStats GetRaceStats();
    IReadOnlyList<TopTenCategory> GetTopTenByCategory();

    bool TryGetEditableResult(int position, out EditResultInput editInput);
    OperationResult UpdateResult(EditResultInput editInput);
    byte[] GenerateResultsPdf();
}
