using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

/// <summary>Admin CRUD for the canonical clubs list (US45 AC9). No hard delete —
/// deactivate instead, so historic entrants still match.</summary>
public class ClubsController : Controller
{
    private readonly IClubService _clubs;

    public ClubsController(IClubService clubs) => _clubs = clubs;

    [HttpGet]
    public IActionResult Index() => View(_clubs.GetClubs());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Add(string name)
    {
        StoreFeedback(_clubs.Add(name));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Rename(int id, string name)
    {
        StoreFeedback(_clubs.Rename(id, name));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Deactivate(int id)
    {
        StoreFeedback(_clubs.SetActive(id, false));
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Reactivate(int id)
    {
        StoreFeedback(_clubs.SetActive(id, true));
        return RedirectToAction(nameof(Index));
    }

    private void StoreFeedback(OperationResult result)
    {
        var lines = new List<string>(result.Messages);
        lines.AddRange(result.Warnings.Select(w => $"Warning: {w}"));
        lines.AddRange(result.Errors.Select(e => $"Error: {e}"));
        TempData["FeedbackType"] = result.Success ? "success" : "danger";
        TempData["FeedbackText"] = string.Join("<br/>", lines);
    }
}
