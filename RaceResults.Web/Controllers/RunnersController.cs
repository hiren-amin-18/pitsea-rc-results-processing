using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class RunnersController : Controller
{
    private readonly IRunnerRegistryService _registry;

    public RunnersController(IRunnerRegistryService registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public IActionResult Index()
    {
        return View(_registry.GetRunners());
    }

    [HttpGet]
    public IActionResult Edit(int id)
    {
        if (!_registry.TryGetRunnerForEdit(id, out var input))
        {
            TempData["FeedbackType"] = "danger";
            TempData["FeedbackText"] = "Runner not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditRunnerInput model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _registry.UpdateRunnerAsync(model);
        StoreFeedback(result);
        if (!result.Success)
        {
            return View(model);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Merge(int sourceId, int targetId)
    {
        var result = await _registry.MergeRunnersAsync(sourceId, targetId);
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
