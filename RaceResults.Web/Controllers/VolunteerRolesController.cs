using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class VolunteerRolesController : Controller
{
    private readonly IVolunteerRoleService _roles;
    private readonly IVolunteerRegistryService _volunteers;

    public VolunteerRolesController(IVolunteerRoleService roles, IVolunteerRegistryService volunteers)
    {
        _roles = roles;
        _volunteers = volunteers;
    }

    [HttpGet]
    public IActionResult Index(EventType eventType = EventType.CrownToCrown, bool includeInactive = false)
    {
        ViewBag.EventType = eventType;
        ViewBag.IncludeInactive = includeInactive;
        return View(_roles.GetRoles(eventType, includeInactive));
    }

    [HttpGet]
    public IActionResult Create(EventType eventType = EventType.CrownToCrown)
    {
        ViewBag.Volunteers = _volunteers.GetVolunteers();
        return View(new VolunteerRoleInput
        {
            EventType = eventType,
            DefaultCount = 1,
            MinCount = 1,
            MaxCount = 1,
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VolunteerRoleInput model)
    {
        if (!ModelState.IsValid) { ViewBag.Volunteers = _volunteers.GetVolunteers(); return View(model); }
        var result = await _roles.CreateAsync(model);
        StoreFeedback(result);
        if (!result.Success) { ViewBag.Volunteers = _volunteers.GetVolunteers(); return View(model); }
        return RedirectToAction(nameof(Index), new { eventType = model.EventType });
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!_roles.TryGetRoleForEdit(id, out var input))
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Role not found.";
            return RedirectToAction(nameof(Index));
        }
        ViewBag.Volunteers = _volunteers.GetVolunteers();
        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(VolunteerRoleInput model)
    {
        if (!ModelState.IsValid) { ViewBag.Volunteers = _volunteers.GetVolunteers(); return View(model); }
        var result = await _roles.UpdateAsync(model);
        StoreFeedback(result);
        if (!result.Success) { ViewBag.Volunteers = _volunteers.GetVolunteers(); return View(model); }
        return RedirectToAction(nameof(Index), new { eventType = model.EventType });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActive(int id, bool isActive, EventType eventType)
    {
        var result = await _roles.SetActiveAsync(id, isActive);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index), new { eventType });
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
