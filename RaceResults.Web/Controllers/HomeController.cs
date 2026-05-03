using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class HomeController : Controller
{
    private readonly IRaceResultsService _raceResultsService;

    public HomeController(IRaceResultsService raceResultsService)
    {
        _raceResultsService = raceResultsService;
    }

    public IActionResult Index()
    {
        var counts = _raceResultsService.GetStatusCounts();
        var model = new HomeDashboardViewModel
        {
            Entrants = counts.EntrantCount,
            FinishBibRows = counts.FinishBibCount,
            TimingRows = counts.TimingCount,
            ResultsRows = counts.FinishBibCount
        };

        return View(model);
    }

    public IActionResult Settings()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
