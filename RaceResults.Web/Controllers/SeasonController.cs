using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class SeasonController : Controller
{
    private readonly ISeasonStatisticsService _season;
    private readonly IRaceResultsService _raceResultsService;

    public SeasonController(ISeasonStatisticsService season, IRaceResultsService raceResultsService)
    {
        _season = season;
        _raceResultsService = raceResultsService;
    }

    [HttpGet]
    public IActionResult Dashboard(int? year)
    {
        var seasonYear = year ?? _raceResultsService.GetCurrentEvent().EventDate.Year;
        return View(_season.GetSeasonDashboard(seasonYear));
    }

    [HttpGet]
    public IActionResult Runner(int id, int? year)
    {
        var seasonYear = year ?? _raceResultsService.GetCurrentEvent().EventDate.Year;
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
