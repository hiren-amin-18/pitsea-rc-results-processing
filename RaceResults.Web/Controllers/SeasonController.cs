using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class SeasonController : Controller
{
    private readonly ISeasonStatisticsService _season;
    private readonly IRaceResultsService _raceResultsService;
    private readonly ISeasonReviewService _review;

    public SeasonController(ISeasonStatisticsService season, IRaceResultsService raceResultsService, ISeasonReviewService review)
    {
        _season = season;
        _raceResultsService = raceResultsService;
        _review = review;
    }

    [HttpGet]
    public IActionResult Review(int? year)
    {
        var seasonYear = year ?? _raceResultsService.GetCurrentEvent()?.EventDate.Year ?? DateTime.Today.Year;
        return View(_review.Build(seasonYear));
    }

    [HttpGet]
    public IActionResult ReviewPdf(int? year)
    {
        var seasonYear = year ?? _raceResultsService.GetCurrentEvent()?.EventDate.Year ?? DateTime.Today.Year;
        var bytes = _review.GeneratePdf(seasonYear);
        return File(bytes, "application/pdf", $"season-review-{seasonYear}.pdf");
    }

    [HttpGet]
    public IActionResult Dashboard(int? year)
    {
        var seasonYear = year ?? _raceResultsService.GetCurrentEvent()?.EventDate.Year ?? DateTime.Today.Year;
        return View(_season.GetSeasonDashboard(seasonYear));
    }

    [HttpGet]
    public IActionResult Stats(int? year)
    {
        var seasonYear = year ?? _raceResultsService.GetCurrentEvent()?.EventDate.Year ?? DateTime.Today.Year;
        return View(_season.GetC2CSeasonStats(seasonYear));
    }

    [HttpGet]
    public IActionResult Runner(int id, int? year)
    {
        var seasonYear = year ?? _raceResultsService.GetCurrentEvent()?.EventDate.Year ?? DateTime.Today.Year;
        var profile = _season.GetRunnerSeasonProfile(id, seasonYear);
        if (profile is null)
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Runner not found.";
            return RedirectToAction(nameof(Dashboard), new { year = seasonYear });
        }

        return View(profile);
    }
}
