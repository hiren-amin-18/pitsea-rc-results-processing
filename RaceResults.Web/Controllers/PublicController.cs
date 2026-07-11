using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

[Route("public")]
public class PublicController : Controller
{
    private readonly IRaceResultsService _raceResultsService;
    private readonly IChampionsOfChampionsService _championsService;

    public PublicController(IRaceResultsService raceResultsService, IChampionsOfChampionsService championsService)
    {
        _raceResultsService = raceResultsService;
        _championsService = championsService;
    }

    [HttpGet("results/{token}")]
    public IActionResult Results(string token)
    {
        var raceEvent = _raceResultsService.GetPublishedEventByToken(token);
        if (raceEvent is null)
        {
            return NotFound();
        }

        var collated = _raceResultsService.GetCollatedResults(raceEvent.Id);
        var winners = _raceResultsService.GetTopTenByCategory(raceEvent.Id);
        ResultRecord? WinnerFor(string category) => winners.FirstOrDefault(c => c.Name == category)?.Results.FirstOrDefault();

        var model = new PublicResultsViewModel
        {
            Event = raceEvent,
            Results = collated,
            DnfEntrants = _raceResultsService.GetDnfEntrants(raceEvent.Id),
            MaleWinner = WinnerFor("Male"),
            FemaleWinner = WinnerFor("Female"),
            MaleU18Winner = WinnerFor("Male U18"),
            FemaleU18Winner = WinnerFor("Female U18"),
            MaleVetWinner = WinnerFor("Vet Male"),
            FemaleVetWinner = WinnerFor("Vet Female"),
            SeasonYear = raceEvent.EventDate.Year
        };

        return View(model);
    }

    [HttpGet("champions/{token}")]
    public async Task<IActionResult> Champions(string token, bool detail = false)
    {
        var raceEvent = _raceResultsService.GetPublishedEventByToken(token);
        if (raceEvent is null)
        {
            return NotFound();
        }

        var seasonYear = raceEvent.EventDate.Year;
        var leaderboard = await _championsService.GetLeaderboardAsync(seasonYear);

        var model = new PublicChampionsViewModel
        {
            Token = token,
            EventName = raceEvent.EventName,
            SeasonYear = seasonYear,
            Leaderboard = leaderboard,
            ShowDetail = detail,
            Detail = detail ? await _championsService.GetLeaderboardDetailAsync(seasonYear) : null
        };

        return View(model);
    }
}
