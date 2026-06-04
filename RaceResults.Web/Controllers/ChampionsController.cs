using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CalculatePoints(int eventId)
    {
        try
        {
            await _championsService.CalculateAndSaveEventPointsAsync(eventId);
            TempData["Success"] = "Points calculated successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Leaderboard));
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
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(20);
                page.Size(PageSizes.A4);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(col =>
                {
                    col.Item().AlignCenter().Text("Champions of Champions Leaderboard").Bold().FontSize(16);
                    col.Item().AlignCenter().Text($"{seasonYear} Season — As of {currentEvent.EventName} ({currentEvent.EventDate:MMMM d, yyyy})").FontSize(11);
                    col.Item().PaddingBottom(10);
                });

                page.Content().Column(column =>
                {
                    column.Spacing(12);

                    var categories = leaderboard.GroupBy(e => e.Category).ToList();

                    foreach (var category in categories)
                    {
                        column.Item().Text(category.Key).Bold().FontSize(13);

                        column.Item().Border(1).BorderColor(Colors.Black).Table(table =>
                        {
                            table.ColumnsDefinition(def =>
                            {
                                def.ConstantColumn(40);  // Rank
                                def.RelativeColumn(3);   // Name
                                def.RelativeColumn(2);   // Club
                                def.ConstantColumn(55);  // Events
                                def.ConstantColumn(55);  // Points
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderStyle).AlignCenter().Text("Rank").SemiBold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).Text("Name").SemiBold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).Text("Club").SemiBold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).AlignCenter().Text("Events").SemiBold().FontColor(Colors.White);
                                header.Cell().Element(HeaderStyle).AlignCenter().Text("Points").SemiBold().FontColor(Colors.White);
                            });

                            foreach (var entry in category.OrderBy(e => e.Rank))
                            {
                                var bgColor = entry.Rank switch
                                {
                                    1 => "#FFD700",
                                    2 => "#C0C0C0",
                                    3 => "#CD7F32",
                                    _ => "#FFFFFF"
                                };
                                var tiedMarker = entry.IsPointsTied ? " †" : "";

                                IContainer CellStyle(IContainer c) => c
                                    .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
                                    .Background(bgColor)
                                    .Padding(4);

                                table.Cell().Element(CellStyle).AlignCenter().Text(entry.Rank.ToString());
                                table.Cell().Element(CellStyle).Text(entry.Entrant?.Name ?? "-");
                                table.Cell().Element(CellStyle).Text(entry.Entrant?.Club ?? "-");
                                table.Cell().Element(CellStyle).AlignCenter().Text(entry.RaceCount.ToString() + tiedMarker);
                                table.Cell().Element(CellStyle).AlignCenter().Text(entry.TotalPoints.ToString());
                            }
                        });
                    }
                });
            });
        });

        return document.GeneratePdf();

        static IContainer HeaderStyle(IContainer container) => container
            .Background(Colors.Blue.Darken3)
            .Border(0.5f).BorderColor(Colors.Black)
            .Padding(4);
    }
}
