using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class RaceController : Controller
{
    private readonly IRaceResultsService _raceResultsService;
    private readonly IChampionsOfChampionsService _championsService;
    private readonly ICourseRecordService _courseRecordService;

    public RaceController(
        IRaceResultsService raceResultsService,
        IChampionsOfChampionsService championsService,
        ICourseRecordService courseRecordService)
    {
        _raceResultsService = raceResultsService;
        _championsService = championsService;
        _courseRecordService = courseRecordService;
    }

    [HttpGet]
    public IActionResult Uploads()
    {
        var counts = _raceResultsService.GetStatusCounts();
        var model = new UploadsViewModel
        {
            EntrantCount = counts.EntrantCount,
            FinishBibCount = counts.FinishBibCount,
            TimingCount = counts.TimingCount
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadEntrants(List<IFormFile> files)
    {
        var result = await _raceResultsService.UploadEntrantsAsync(files);
        StoreFeedback(result);
        return RedirectToAction(nameof(Uploads));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadFinishBib(IFormFile file)
    {
        var result = await _raceResultsService.UploadFinishBibAsync(file);
        StoreFeedback(result);
        return RedirectToAction(nameof(Uploads));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadTimings(IFormFile file)
    {
        var result = await _raceResultsService.UploadTimingsAsync(file);
        StoreFeedback(result);

        // Auto-calculate Champions of Champions points if this is a Crown to Crown event
        if (result.Success)
        {
            var currentEvent = _raceResultsService.GetCurrentEvent();
            if (currentEvent is not null && currentEvent.EventType == EventType.CrownToCrown)
            {
                try
                {
                    await _championsService.CalculateAndSaveEventPointsAsync(currentEvent.Id);
                }
                catch (InvalidOperationException)
                {
                    // Non-critical: log but don't block the upload response
                }
            }
        }

        return RedirectToAction(nameof(Uploads));
    }

    [HttpGet]
    public IActionResult Results(int? eventId)
    {
        var currentEvent = _raceResultsService.GetCurrentEvent();
        if (currentEvent is null && !eventId.HasValue)
        {
            return RedirectToAction("Index", "Events");
        }
        var viewed = eventId.HasValue
            ? _raceResultsService.GetEvents().FirstOrDefault(e => e.Id == eventId.Value) ?? currentEvent
            : currentEvent;
        if (viewed is null)
        {
            return RedirectToAction("Index", "Events");
        }
        var readOnly = currentEvent is null || viewed.Id != currentEvent.Id;

        var model = new ResultsPageViewModel
        {
            Results = _raceResultsService.GetCollatedResults(viewed.Id).ToList(),
            DnfEntrants = _raceResultsService.GetDnfEntrants(viewed.Id).ToList(),
            DnsEntrants = _raceResultsService.GetDnsEntrants(viewed.Id).ToList(),
            DsqResults = _raceResultsService.GetDsqResults(viewed.Id).ToList(),
            // Course-record confirmation only applies to the current (editable) event.
            PendingCourseRecords = readOnly ? new() : _courseRecordService.GetPendingRecords(currentEvent!.Id).ToList(),
            IsReadOnly = readOnly,
            ViewedEventId = viewed.Id,
            ViewedEventName = viewed.EventName,
            IsArchived = viewed.IsArchived
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmCourseRecord(string category)
    {
        var currentEvent = _raceResultsService.GetCurrentEvent();
        if (currentEvent is null)
        {
            return RedirectToAction("Index", "Events");
        }
        var result = _courseRecordService.ConfirmRecord(currentEvent.Id, category);
        StoreFeedback(result);
        return RedirectToAction(nameof(Results));
    }

    [HttpGet]
    public IActionResult Disqualify(int position)
    {
        return View(new DisqualifyInput { Position = position });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Disqualify(DisqualifyInput model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = _raceResultsService.DisqualifyResult(model.Position, model.Reason);
        StoreFeedback(result);
        if (!result.Success)
        {
            return View(model);
        }

        var currentEvent = _raceResultsService.GetCurrentEvent();
        if (currentEvent is not null && currentEvent.EventType == EventType.CrownToCrown)
        {
            try
            {
                await _championsService.VoidDisqualifiedAndRecalculateAsync(currentEvent.Id);
            }
            catch (InvalidOperationException)
            {
                // Non-critical: scoring is out of season or otherwise not applicable.
            }
        }

        return RedirectToAction(nameof(Results));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reinstate(int position)
    {
        var result = _raceResultsService.ReinstateResult(position);
        StoreFeedback(result);

        if (result.Success)
        {
            var currentEvent = _raceResultsService.GetCurrentEvent();
            if (currentEvent is not null && currentEvent.EventType == EventType.CrownToCrown)
            {
                try
                {
                    await _championsService.RecalculateSeasonPointsAsync(currentEvent.EventDate.Year);
                }
                catch (InvalidOperationException)
                {
                    // Non-critical.
                }
            }
        }

        return RedirectToAction(nameof(Results));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetStatus(string bibNumber, FinishStatus status)
    {
        var result = _raceResultsService.SetNonFinisherStatus(bibNumber, status);
        StoreFeedback(result);
        return RedirectToAction(nameof(Results));
    }

    [HttpGet]
    public IActionResult EditResult(int position)
    {
        if (!_raceResultsService.TryGetEditableResult(position, out var model))
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Result row not found.";
            return RedirectToAction(nameof(Results));
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditResult(EditResultInput model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = _raceResultsService.UpdateResult(model);
        StoreFeedback(result);

        if (!result.Success)
        {
            return View(model);
        }

        // Recalculate Champions of Champions if this is a Crown to Crown event
        var currentEvent = _raceResultsService.GetCurrentEvent();
        if (currentEvent is not null && currentEvent.EventType == EventType.CrownToCrown)
        {
            try
            {
                await _championsService.RecalculateSeasonPointsAsync(currentEvent.EventDate.Year);
            }
            catch (InvalidOperationException)
            {
                // Non-critical
            }
        }

        return RedirectToAction(nameof(Results));
    }

    [HttpGet]
    public IActionResult Stats(int? eventId)
    {
        var viewedId = eventId ?? _raceResultsService.GetCurrentEvent()?.Id;
        if (viewedId is null)
        {
            return RedirectToAction("Index", "Events");
        }
        var stats = _raceResultsService.GetRaceStats(viewedId.Value);
        var results = _raceResultsService.GetCollatedResults(viewedId.Value);

        var clubBreakdown = results
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Club) ? "Unaffiliated" : r.Club, StringComparer.OrdinalIgnoreCase)
            .Select(g => new BreakdownItem { Label = g.Key, Value = g.Count() })
            .OrderByDescending(x => x.Value)
            .ThenBy(x => x.Label)
            .ToList();

        // Derive from typed durations (US17): every finisher with a timing row has one, so no row is silently dropped.
        var finishersPerMinute = results
            .Where(r => r.Duration.HasValue)
            .Select(r => (int)Math.Floor(r.Duration!.Value.TotalMinutes))
            .GroupBy(minute => minute)
            .Select(g => new BreakdownItem { Label = g.Key.ToString(), Value = g.Count() })
            .OrderBy(x => int.Parse(x.Label))
            .ToList();

        var model = new RaceStatsDashboardViewModel
        {
            Stats = stats,
            Summary = _raceResultsService.GetRaceStatisticsSummary(viewedId.Value),
            ClubBreakdown = clubBreakdown,
            FinishersPerMinute = finishersPerMinute
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Top10(int? eventId)
    {
        var viewedId = eventId ?? _raceResultsService.GetCurrentEvent()?.Id;
        if (viewedId is null)
        {
            return RedirectToAction("Index", "Events");
        }
        var model = _raceResultsService.GetTopTenByCategory(viewedId.Value);
        return View(model);
    }

    [HttpGet]
    public IActionResult ExportPdf(int? eventId)
    {
        var viewedId = eventId ?? _raceResultsService.GetCurrentEvent()?.Id;
        if (viewedId is null)
        {
            return RedirectToAction("Index", "Events");
        }
        var bytes = _raceResultsService.GenerateResultsPdf(viewedId.Value);
        return File(bytes, "application/pdf", "race-results.pdf");
    }

    [HttpGet]
    public IActionResult ExportCsv(int? eventId)
    {
        var viewedId = eventId ?? _raceResultsService.GetCurrentEvent()?.Id;
        if (viewedId is null)
        {
            return RedirectToAction("Index", "Events");
        }
        var bytes = _raceResultsService.GenerateResultsCsv(viewedId.Value);
        return File(bytes, "text/csv", _raceResultsService.GetResultsCsvFileName(viewedId.Value));
    }

    private void StoreFeedback(OperationResult result)
    {
        var lines = new List<string>();
        lines.AddRange(result.Messages);
        lines.AddRange(result.Warnings.Select(w => $"Warning: {w}"));
        lines.AddRange(result.Errors.Select(e => $"Error: {e}"));

        TempData["FeedbackType"] = result.Success ? (result.Warnings.Count > 0 ? "warning" : "success") : "danger";
        TempData["FeedbackText"] = string.Join("<br/>", lines);
    }
}
