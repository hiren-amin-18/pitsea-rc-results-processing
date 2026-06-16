using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;
using static RaceResults.Web.Services.SeasonCalendar;

namespace RaceResults.Web.Controllers;

public class EventsController : Controller
{
    private readonly IRaceResultsService _raceResultsService;
    private readonly ISeasonCalendarService _seasonCalendar;

    public EventsController(IRaceResultsService raceResultsService, ISeasonCalendarService seasonCalendar)
    {
        _raceResultsService = raceResultsService;
        _seasonCalendar = seasonCalendar;
    }

    [HttpGet]
    public IActionResult GenerateSeason(int? year, SeasonCalendar.SeptemberOption septemberOption = SeasonCalendar.SeptemberOption.SecondWednesday)
    {
        var resolvedYear = year ?? DateTime.Today.Year;
        ViewBag.Year = resolvedYear;
        ViewBag.SeptemberOption = septemberOption;
        var preview = _seasonCalendar.Preview(resolvedYear, septemberOption);
        return View(preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ActionName("GenerateSeason")]
    public IActionResult GenerateSeasonConfirm(int year, SeasonCalendar.SeptemberOption septemberOption)
    {
        var result = _seasonCalendar.Generate(year, septemberOption);
        var lines = new List<string> { $"Created {result.CreatedCount} Crown to Crown event(s) for {year}." };
        if (result.SkippedDates.Count > 0)
        {
            lines.Add($"Skipped {result.SkippedDates.Count} date(s) that already had a Crown to Crown event: {string.Join(", ", result.SkippedDates)}.");
        }
        TempData["FeedbackType"] = result.SkippedDates.Count > 0 ? "warning" : "success";
        TempData["FeedbackText"] = string.Join("<br/>", lines);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Index()
    {
        var current = _raceResultsService.GetCurrentEvent();
        var model = new EventsPageViewModel
        {
            CurrentEventId = current?.Id,
            Events = _raceResultsService.GetEvents().OrderBy(e => e.EventDate).ThenBy(e => e.EventName).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreateEventInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CreateEventInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = _raceResultsService.CreateEvent(input);
        StoreFeedback(result);
        if (!result.Success)
        {
            return View(input);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        var raceEvent = _raceResultsService.GetEvents().FirstOrDefault(e => e.Id == id);
        if (raceEvent is null)
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Event not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(new EditEventInput
        {
            Id = raceEvent.Id,
            EventName = raceEvent.EventName,
            EventDate = raceEvent.EventDate,
            EventType = raceEvent.EventType
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(EditEventInput input)
    {
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        var result = _raceResultsService.UpdateEvent(input);
        StoreFeedback(result);
        if (!result.Success)
        {
            return View(input);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SetCurrent(int id)
    {
        var result = _raceResultsService.SetCurrentEvent(id);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var result = _raceResultsService.DeleteEvent(id);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Archive(int id)
    {
        var result = _raceResultsService.ArchiveEvent(id);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Unarchive(int id)
    {
        var result = _raceResultsService.UnarchiveEvent(id);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Publish(int id)
    {
        var result = _raceResultsService.PublishEvent(id);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Unpublish(int id)
    {
        var result = _raceResultsService.UnpublishEvent(id);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index));
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
