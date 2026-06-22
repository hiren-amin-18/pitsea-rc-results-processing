using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

[Route("Events/{eventId:int}/Roster")]
public class VolunteerRosterController : Controller
{
    private readonly IVolunteerRosterService _roster;
    private readonly IVolunteerRosterExportService _export;
    private readonly IVolunteerRegistryService _volunteers;
    private readonly IVolunteerRoleService _roles;
    private readonly IRosterAllocator _allocator;
    private readonly IRosterDraftApplier _applier;
    private readonly IVolunteerRosterImportService _import;

    public VolunteerRosterController(
        IVolunteerRosterService roster,
        IVolunteerRosterExportService export,
        IVolunteerRegistryService volunteers,
        IVolunteerRoleService roles,
        IRosterAllocator allocator,
        IRosterDraftApplier applier,
        IVolunteerRosterImportService import)
    {
        _roster = roster;
        _export = export;
        _volunteers = volunteers;
        _roles = roles;
        _allocator = allocator;
        _applier = applier;
        _import = import;
    }

    [HttpGet("")]
    public IActionResult Index(int eventId)
    {
        var roster = _roster.GetRoster(eventId);
        ViewBag.Volunteers = _volunteers.GetVolunteers();
        ViewBag.Roles = _roles.GetRoles(roster.Event.EventType);
        return View(roster);
    }

    [HttpPost("Add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(VolunteerAssignmentInput input)
    {
        var result = await _roster.AddAssignmentAsync(input);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index), new { eventId = input.EventId });
    }

    [HttpPost("Remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int eventId, int assignmentId)
    {
        var result = await _roster.RemoveAssignmentAsync(assignmentId);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpPost("CopyPrevious")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyPrevious(int eventId)
    {
        var result = await _roster.CopyFromPreviousEventAsync(eventId);
        var feedbackLines = new List<string>();
        if (result.Success)
        {
            feedbackLines.Add($"Copied {result.CopiedCount} assignment(s) from the previous event.");
            foreach (var skipped in result.SkippedItems) feedbackLines.Add($"Warning: skipped {skipped}");
            TempData["FeedbackType"] = result.SkippedItems.Count > 0 ? "warning" : "success";
        }
        else
        {
            feedbackLines.Add($"Error: {result.ErrorMessage}");
            TempData["FeedbackType"] = "danger";
        }
        TempData["FeedbackText"] = string.Join("<br/>", feedbackLines);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpGet("Allocate")]
    public IActionResult Allocate(int eventId)
    {
        var roster = _roster.GetRoster(eventId);
        ViewBag.Volunteers = _volunteers.GetVolunteers();
        ViewBag.Roles = _roles.GetRoles(roster.Event.EventType);
        ViewBag.Event = roster.Event;
        return View(new AllocationFormInput { EventId = eventId });
    }

    [HttpPost("Allocate")]
    [ValidateAntiForgeryToken]
    public IActionResult Allocate(AllocationFormInput input)
    {
        var draft = _allocator.Propose(input.EventId, input.Candidates ?? new List<AllocationCandidate>());
        return View("Draft", draft);
    }

    [HttpPost("Apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Apply(int eventId, [FromForm] string draftJson)
    {
        AllocationDraft? draft;
        try { draft = System.Text.Json.JsonSerializer.Deserialize<AllocationDraft>(draftJson); }
        catch { draft = null; }
        if (draft is null)
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Could not read the draft to apply.";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        var result = await _applier.ApplyAsync(draft);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index), new { eventId });
    }

    [HttpGet("Import")]
    public IActionResult Import(int eventId)
    {
        var roster = _roster.GetRoster(eventId);
        ViewBag.Event = roster.Event;
        ViewBag.HasExistingAssignments = roster.TotalAssigned > 0;
        return View();
    }

    [HttpPost("Import")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(8 * 1024 * 1024)]
    public IActionResult Import(int eventId, IFormFile file)
    {
        if (file is null || file.Length == 0)
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Pick a spreadsheet to upload.";
            return RedirectToAction(nameof(Import), new { eventId });
        }
        using var stream = file.OpenReadStream();
        var preview = _import.BuildPreview(eventId, stream);
        return View("ImportPreview", preview);
    }

    [HttpPost("ImportCommit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportCommit(RosterImportCommitInput input)
    {
        var result = await _import.CommitAsync(input);
        StoreFeedback(result);
        return RedirectToAction(nameof(Index), new { eventId = input.EventId });
    }

    [HttpGet("Pdf")]
    public IActionResult Pdf(int eventId)
    {
        var pdf = _export.ExportPdf(eventId);
        return File(pdf, "application/pdf", $"volunteer-roster-event-{eventId}.pdf");
    }

    [HttpGet("Excel")]
    public IActionResult Excel(int eventId)
    {
        var xlsx = _export.ExportExcel(eventId);
        return File(xlsx, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"volunteer-roster-event-{eventId}.xlsx");
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
