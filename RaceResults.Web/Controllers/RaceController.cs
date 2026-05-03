using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class RaceController : Controller
{
    private readonly IRaceResultsService _raceResultsService;

    public RaceController(IRaceResultsService raceResultsService)
    {
        _raceResultsService = raceResultsService;
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
        return RedirectToAction(nameof(Uploads));
    }

    [HttpGet]
    public IActionResult Results()
    {
        var model = new ResultsPageViewModel
        {
            Results = _raceResultsService.GetCollatedResults().ToList(),
            DnfEntrants = _raceResultsService.GetDnfEntrants().ToList()
        };

        return View(model);
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
    public IActionResult EditResult(EditResultInput model)
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

        return RedirectToAction(nameof(Results));
    }

    [HttpGet]
    public IActionResult Stats()
    {
        var model = _raceResultsService.GetRaceStats();
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
