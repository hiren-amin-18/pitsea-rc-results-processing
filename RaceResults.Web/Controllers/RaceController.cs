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
            if (currentEvent.EventType == EventType.CrownToCrown)
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
    public IActionResult Results()
    {
        var currentEvent = _raceResultsService.GetCurrentEvent();
        var model = new ResultsPageViewModel
        {
            Results = _raceResultsService.GetCollatedResults().ToList(),
            DnfEntrants = _raceResultsService.GetDnfEntrants().ToList(),
            DnsEntrants = _raceResultsService.GetDnsEntrants().ToList(),
            DsqResults = _raceResultsService.GetDsqResults().ToList(),
            PendingCourseRecords = _courseRecordService.GetPendingRecords(currentEvent.Id).ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmCourseRecord(string category)
    {
        var currentEvent = _raceResultsService.GetCurrentEvent();
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
        if (currentEvent.EventType == EventType.CrownToCrown)
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
            if (currentEvent.EventType == EventType.CrownToCrown)
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
        if (currentEvent.EventType == EventType.CrownToCrown)
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
    public IActionResult Stats()
    {
        var stats = _raceResultsService.GetRaceStats();
        var results = _raceResultsService.GetCollatedResults();

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
            Summary = _raceResultsService.GetRaceStatisticsSummary(),
            ClubBreakdown = clubBreakdown,
            FinishersPerMinute = finishersPerMinute
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Top10()
    {
        var model = _raceResultsService.GetTopTenByCategory();
        return View(model);
    }

    [HttpGet]
    public IActionResult ExportPdf()
    {
        var bytes = _raceResultsService.GenerateResultsPdf();
        return File(bytes, "application/pdf", "race-results.pdf");
    }

    [HttpGet]
    public IActionResult ExportCsv()
    {
        var bytes = _raceResultsService.GenerateResultsCsv();
        return File(bytes, "text/csv", _raceResultsService.GetResultsCsvFileName());
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
