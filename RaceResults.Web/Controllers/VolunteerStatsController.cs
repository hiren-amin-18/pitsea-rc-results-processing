using System.Globalization;
using System.Text;
using CsvHelper;
using Microsoft.AspNetCore.Mvc;
using RaceResults.Web.Services;

namespace RaceResults.Web.Controllers;

public class VolunteerStatsController : Controller
{
    private readonly IVolunteerStatsService _stats;

    public VolunteerStatsController(IVolunteerStatsService stats)
    {
        _stats = stats;
    }

    [HttpGet]
    public IActionResult Index(int? year)
    {
        var years = _stats.GetAvailableYears();
        var selectedYear = year ?? (years.Count > 0 ? years[0] : DateTime.Today.Year);
        var model = _stats.GetSeasonStats(selectedYear);
        if (model.AvailableYears.Count == 0) model.AvailableYears.Add(selectedYear);
        return View(model);
    }

    [HttpGet]
    public IActionResult Csv(int year)
    {
        var model = _stats.GetSeasonStats(year);

        using var memory = new MemoryStream();
        using (var writer = new StreamWriter(memory, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true)))
        using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            foreach (var heading in new[] {
                "Name", "Member", "EventsAttended", "Assignments", "BallotEntries",
                "RunAfterCount", "EverPresent", "RolesPerformed",
                "RunCount", "VolunteerCount", "EventsInvolvedIn"
            })
            {
                csv.WriteField(heading);
            }
            csv.NextRecord();

            foreach (var p in model.VolunteerProfiles)
            {
                csv.WriteField(p.Name);
                csv.WriteField(p.IsClubMember ? "Yes" : "No");
                csv.WriteField(p.EventsAttended.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(p.Assignments.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(p.BallotEntries.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(p.RunAfterCount.ToString(CultureInfo.InvariantCulture));
                csv.WriteField(p.IsEverPresent ? "Yes" : "No");
                csv.WriteField(string.Join("; ", p.RolesPerformed.Select(r => $"{r.RoleName} x{r.Times}")));
                csv.WriteField(p.RunAndVolunteer?.RunCount.ToString(CultureInfo.InvariantCulture) ?? "");
                csv.WriteField(p.RunAndVolunteer?.VolunteerCount.ToString(CultureInfo.InvariantCulture) ?? "");
                csv.WriteField(p.RunAndVolunteer?.EventsInvolvedIn.ToString(CultureInfo.InvariantCulture) ?? "");
                csv.NextRecord();
            }
        }

        return File(memory.ToArray(), "text/csv", $"volunteer-stats-{year}.csv");
    }
}
