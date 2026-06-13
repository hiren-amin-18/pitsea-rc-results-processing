using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class CourseRecordsController : Controller
{
    private readonly ICourseRecordService _service;

    public CourseRecordsController(ICourseRecordService service)
    {
        _service = service;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(_service.GetCurrentRecordSlots());
    }

    [HttpGet]
    public IActionResult Edit(EventType eventType, string category)
    {
        return View(_service.GetRecordForEdit(eventType, category));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(EditCourseRecordInput model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = _service.UpsertRecord(model);
        StoreFeedback(result);
        if (!result.Success)
        {
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    private void StoreFeedback(OperationResult result)
    {
        var lines = new List<string>();
        lines.AddRange(result.Messages);
        lines.AddRange(result.Errors.Select(e => $"Error: {e}"));

        TempData["FeedbackType"] = result.Success ? "success" : "danger";
        TempData["FeedbackText"] = string.Join("<br/>", lines);
    }
}
