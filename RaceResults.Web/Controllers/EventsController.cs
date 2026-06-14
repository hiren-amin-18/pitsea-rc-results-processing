using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class EventsController : Controller
{
    private readonly IRaceResultsService _raceResultsService;

    public EventsController(IRaceResultsService raceResultsService)
    {
        _raceResultsService = raceResultsService;
    }

    [HttpGet]
    public IActionResult Index()
    {
        var current = _raceResultsService.GetCurrentEvent();
        var model = new EventsPageViewModel
        {
            CurrentEventId = current.Id,
            Events = _raceResultsService.GetEvents().ToList()
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
