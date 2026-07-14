using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

/// <summary>Per-event action that turns two registration-platform CSVs into the club's
/// Online Registration spreadsheet (US45). C2C-only, gated in the view and in the service.</summary>
[Route("Events/{eventId:int}/OnlineRegistration")]
public class OnlineRegistrationController : Controller
{
    private readonly IOnlineRegistrationGenerator _generator;
    private readonly IRaceResultsService _events;

    public OnlineRegistrationController(IOnlineRegistrationGenerator generator, IRaceResultsService events)
    {
        _generator = generator;
        _events = events;
    }

    [HttpGet("")]
    public IActionResult Index(int eventId)
    {
        var raceEvent = _events.GetEvents().FirstOrDefault(e => e.Id == eventId);
        if (raceEvent is null) return NotFound();
        if (raceEvent.EventType != EventType.CrownToCrown)
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Online Registration generation is only available for Crown to Crown events.";
            return RedirectToAction("Index", "Events");
        }
        ViewBag.Event = raceEvent;
        return View();
    }

    [HttpPost("Preview")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(16 * 1024 * 1024)]
    public IActionResult Preview(int eventId, IFormFile? adults, IFormFile? u18)
    {
        var raceEvent = _events.GetEvents().FirstOrDefault(e => e.Id == eventId);
        if (raceEvent is null) return NotFound();

        Stream? adultsStream = null;
        Stream? u18Stream = null;
        try
        {
            adultsStream = adults?.Length > 0 ? adults.OpenReadStream() : null;
            u18Stream = u18?.Length > 0 ? u18.OpenReadStream() : null;
            var preview = _generator.BuildPreview(eventId, adultsStream, u18Stream);
            ViewBag.Event = raceEvent;
            return View("Preview", preview);
        }
        finally
        {
            adultsStream?.Dispose();
            u18Stream?.Dispose();
        }
    }

    [HttpPost("Generate")]
    [ValidateAntiForgeryToken]
    public IActionResult Generate(OnlineRegistrationGenerateInput input)
    {
        var result = _generator.Generate(input);
        if (!result.Success)
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = string.Join("<br/>", result.Errors);
            return RedirectToAction(nameof(Index), new { eventId = input.EventId });
        }
        return File(result.Bytes!,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            result.FileName ?? "online-registration.xlsx");
    }
}
