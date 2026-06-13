using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class HomeController : Controller
{
    private readonly IRaceResultsService _raceResultsService;
    private readonly IDatabaseBackupService _backupService;

    public HomeController(IRaceResultsService raceResultsService, IDatabaseBackupService backupService)
    {
        _raceResultsService = raceResultsService;
        _backupService = backupService;
    }

    public IActionResult Index()
    {
        var counts = _raceResultsService.GetStatusCounts();
        var currentEvent = _raceResultsService.GetCurrentEvent();
        var model = new HomeDashboardViewModel
        {
            CurrentEventName = currentEvent.EventName,
            CurrentEventDate = currentEvent.EventDate,
            Entrants = counts.EntrantCount,
            FinishBibRows = counts.FinishBibCount,
            TimingRows = counts.TimingCount,
            ResultsRows = counts.FinishBibCount
        };

        return View(model);
    }

    public IActionResult Settings()
    {
        return View();
    }

    [HttpGet]
    public IActionResult DownloadBackup()
    {
        var bytes = _backupService.CreateBackup();
        var fileName = _backupService.GetBackupFileName(DateTime.Now);
        return File(bytes, "application/octet-stream", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(200_000_000)]
    public IActionResult RestoreBackup(IFormFile? backupFile, bool confirmRestore)
    {
        if (!confirmRestore)
        {
            TempData["BackupFeedbackType"] = "warning";
            TempData["BackupFeedbackText"] = "Please tick the confirmation box to acknowledge that current data will be replaced.";
            return RedirectToAction(nameof(Settings));
        }

        if (backupFile is null || backupFile.Length == 0)
        {
            TempData["BackupFeedbackType"] = "danger";
            TempData["BackupFeedbackText"] = "Please choose a backup file to restore.";
            return RedirectToAction(nameof(Settings));
        }

        using var stream = backupFile.OpenReadStream();
        var result = _backupService.Restore(stream);

        TempData["BackupFeedbackType"] = result.Success ? "success" : "danger";
        TempData["BackupFeedbackText"] = result.Message;
        return RedirectToAction(nameof(Settings));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
