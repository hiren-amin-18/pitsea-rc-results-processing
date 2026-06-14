using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class VolunteersController : Controller
{
    private readonly IVolunteerRegistryService _registry;
    private readonly IRunnerRegistryService _runners;

    public VolunteersController(IVolunteerRegistryService registry, IRunnerRegistryService runners)
    {
        _registry = registry;
        _runners = runners;
    }

    [HttpGet]
    public IActionResult Index(bool showInactive = false)
    {
        ViewBag.ShowInactive = showInactive;
        return View(_registry.GetVolunteers(showInactive));
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewBag.Runners = _runners.GetRunners();
        return View(new VolunteerInput { IsClubMember = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VolunteerInput model)
    {
        if (!ModelState.IsValid) { ViewBag.Runners = _runners.GetRunners(); return View(model); }
        var result = await _registry.CreateAsync(model);
        StoreFeedback(result);
        if (!result.Success) { ViewBag.Runners = _runners.GetRunners(); return View(model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!_registry.TryGetVolunteerForEdit(id, out var input))
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Volunteer not found.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Runners = _runners.GetRunners();
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VolunteerInput model)
    {
        if (!ModelState.IsValid) { ViewBag.Runners = _runners.GetRunners(); return View(model); }
        var result = await _registry.UpdateAsync(model);
        StoreFeedback(result);
        if (!result.Success) { ViewBag.Runners = _runners.GetRunners(); return View(model); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive)
    {
        var result = await _registry.SetActiveAsync(id, isActive);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index), new { showInactive = !isActive });
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
