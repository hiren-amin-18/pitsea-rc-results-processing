using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class ChampionsController : Controller
{
    private readonly IChampionsOfChampionsService _championsService;
    private readonly IRaceResultsService _raceResultsService;

    public ChampionsController(
        IChampionsOfChampionsService championsService,
        IRaceResultsService raceResultsService)
    {
        _championsService = championsService;
        _raceResultsService = raceResultsService;
    }

    [HttpGet]
    public async Task<IActionResult> Leaderboard(int? eventId, int? year)
    {
        int seasonYear = year ?? DateTime.Now.Year;
        var currentEvent = _raceResultsService.GetCurrentEvent();
        var leaderboard = await _championsService.GetLeaderboardAsync(seasonYear, eventId);

        var model = new ChampionsLeaderboardViewModel
        {
            Leaderboard = leaderboard,
            SeasonYear = seasonYear,
            CurrentEventId = eventId ?? currentEvent.Id,
            CurrentEventName = currentEvent.EventName,
            AsOfDate = currentEvent.EventDate
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> ExportPdf(int? eventId, int? year)
    {
        int seasonYear = year ?? DateTime.Now.Year;
        var currentEvent = _raceResultsService.GetCurrentEvent();
        var leaderboard = await _championsService.GetLeaderboardAsync(seasonYear, eventId);

        // Generate PDF with leaderboard
        // PDF should include: Rank, Name, Club, Events (with † for tied runners), Points
        // Top 3 should have gold/silver/bronze backgrounds
        var bytes = GenerateLeaderboardPdf(leaderboard, currentEvent, seasonYear);
        return File(bytes, "application/pdf", $"champions-of-champions-{seasonYear}-{DateTime.Now:yyyyMMdd}.pdf");
    }

    private byte[] GenerateLeaderboardPdf(IReadOnlyList<ChampionsLeaderboardEntry> leaderboard, RaceEvent currentEvent, int seasonYear)
    {
        // For now, return empty PDF bytes - this will be implemented with QuestPDF
        // following the same pattern as RaceResultsService.GenerateResultsPdf()
        // 
        // PDF should include:
        // - Title: "Champions of Champions Leaderboard"
        // - Season year and event date/name
        // - For each category:
        //   - Category header
        //   - Table: Rank | Name | Club | Events | Points
        //   - Runners with tied points marked with † 
        //   - Top 3 with gold/silver/bronze background colors
        return System.Text.Encoding.UTF8.GetBytes("%PDF-1.4\n");
    }
}
