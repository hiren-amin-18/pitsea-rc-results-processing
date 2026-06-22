using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class VolunteerRosterImportTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly VolunteerRosterImportService _import;
    private readonly int _eventId;

    public VolunteerRosterImportTests()
    {
        (var factory, _connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _import = new VolunteerRosterImportService(factory, NullLogger<VolunteerRosterImportService>.Instance);

        using var db = _factory.CreateDbContext();
        var ev = new RaceEvent { EventName = "Good Friday 2026", EventDate = new DateTime(2026, 4, 3), EventType = EventType.CrownToCrown };
        db.Events.Add(ev);
        db.SaveChanges();
        _eventId = ev.Id;
    }

    public void Dispose() => _connection.Dispose();

    // ----- Parser unit tests -----

    [Theory]
    [InlineData("Sue Allen (to run)", "Sue Allen", true, null)]
    [InlineData("Nadine Baldwin (finish)", "Nadine Baldwin", false, "finish")]
    [InlineData("Katie Darby (to run) (course)", "Katie Darby", true, "course")]
    [InlineData("Hiren Amin", "Hiren Amin", false, null)]
    [InlineData("  Hiren  Amin  ", "Hiren Amin", false, null)]
    public void ExtractAnnotations_ParsesCorrectly(string raw, string name, bool willRun, string? note)
    {
        var (n, wr, nt) = VolunteerRosterImportService.ExtractAnnotations(raw);
        Assert.Equal(name, n);
        Assert.Equal(willRun, wr);
        Assert.Equal(note, nt);
    }

    // ----- Preview / Commit flow -----

    [Fact]
    public void Preview_MatchesExistingVolunteer_DoesNotOverrideFields()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Volunteers.Add(new Volunteer { Name = "Hiren Amin", Gender = "Male", IsClubMember = true, IsFirstAidTrained = true });
            db.SaveChanges();
        }

        var preview = _import.BuildPreview(_eventId, BuildSheet(("Lead", "Hiren Amin")));

        Assert.Empty(preview.NewVolunteers);
        var matched = Assert.Single(preview.MatchedVolunteers);
        Assert.Equal("Hiren Amin", matched.Name);
        Assert.Single(preview.Assignments);
    }

    [Fact]
    public void Preview_CaseAndWhitespace_AreNormalisedForMatching()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Volunteers.Add(new Volunteer { Name = "Hiren Amin" });
            db.SaveChanges();
        }

        var preview = _import.BuildPreview(_eventId, BuildSheet(("Lead", "  hiren   amin  ")));
        Assert.Empty(preview.NewVolunteers);
        Assert.Single(preview.MatchedVolunteers);
    }

    [Fact]
    public void Preview_NewVolunteer_IsListedForGenderSelection()
    {
        var preview = _import.BuildPreview(_eventId, BuildSheet(("Lead", "Alice Example")));
        var n = Assert.Single(preview.NewVolunteers);
        Assert.Equal("Alice Example", n.Name);
        Assert.Equal("alice example", n.NameKey);
    }

    [Fact]
    public void Preview_MarshalAlias_MapsToMarshalPoint()
    {
        var preview = _import.BuildPreview(_eventId, BuildSheet(
            ("Marshal (1)", "Adi One"),
            ("Marshal (5a)", "Adi Two")));
        Assert.Empty(preview.UnmatchedRoles);
        Assert.Equal(2, preview.Assignments.Count);
        Assert.Contains(preview.Assignments, a => a.RoleName == "Marshal Point 1");
        Assert.Contains(preview.Assignments, a => a.RoleName == "Marshal Point 5a");
    }

    [Fact]
    public void Preview_UnknownRole_IsReportedNotAssigned()
    {
        var preview = _import.BuildPreview(_eventId, BuildSheet(
            ("Lead", "Hiren Amin"),
            ("Bouncy Castle Manager", "Someone")));
        Assert.Single(preview.Assignments);
        Assert.Contains("Bouncy Castle Manager", preview.UnmatchedRoles);
    }

    [Fact]
    public void Preview_BlankVolunteerCell_IsSkipped()
    {
        var preview = _import.BuildPreview(_eventId, BuildSheet(
            ("Shadow Lead", ""),
            ("Lead", "Hiren Amin")));
        Assert.Single(preview.Assignments);
        Assert.Empty(preview.UnmatchedRoles);
    }

    [Fact]
    public void Preview_RefusesIfRosterAlreadyHasAssignments()
    {
        // Pre-seed an assignment via DB.
        using (var db = _factory.CreateDbContext())
        {
            var role = db.VolunteerRoles.First(r => r.EventType == EventType.CrownToCrown);
            var v = new Volunteer { Name = "Already Here" };
            db.Volunteers.Add(v);
            db.SaveChanges();
            db.VolunteerAssignments.Add(new VolunteerAssignment { EventId = _eventId, VolunteerId = v.Id, VolunteerRoleId = role.Id });
            db.SaveChanges();
        }

        var preview = _import.BuildPreview(_eventId, BuildSheet(("Lead", "Hiren Amin")));
        Assert.True(preview.RosterAlreadyHasAssignments);
        Assert.False(preview.CanCommit);
    }

    [Fact]
    public async Task Commit_CreatesNewVolunteersAndAssignments_AndDoesNotOverrideExisting()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Volunteers.Add(new Volunteer { Name = "Hiren Amin", Gender = "Male", IsClubMember = true, IsFirstAidTrained = true });
            db.SaveChanges();
        }

        var preview = _import.BuildPreview(_eventId, BuildSheet(
            ("Lead", "Hiren Amin"),
            ("Timekeeping", "Liz Cox\nPaul Reed"),
            ("Number Collection", "Ken Carey\nSue Allen (to run)"),
            ("First Aid and Prizes", "Nadine Baldwin (finish)\nBeth Gadlin (course)")));

        var input = new RosterImportCommitInput
        {
            EventId = _eventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview),
            NewVolunteerGenders = new Dictionary<string, string>
            {
                ["liz cox"] = "Female",
                ["paul reed"] = "Male",
                ["ken carey"] = "Male",
                ["sue allen"] = "Female",
                ["nadine baldwin"] = "Female",
                ["beth gadlin"] = "Female"
            }
        };
        var result = await _import.CommitAsync(input);
        Assert.True(result.Success);

        using var db2 = _factory.CreateDbContext();
        var assignments = db2.VolunteerAssignments
            .Include(a => a.Volunteer)
            .Include(a => a.VolunteerRole)
            .Where(a => a.EventId == _eventId)
            .ToList();
        Assert.Equal(7, assignments.Count);

        // Sue Allen got the run-after flag.
        var sue = assignments.Single(a => a.Volunteer!.Name == "Sue Allen");
        Assert.True(sue.WillRunAfter);

        // Nadine stays at "First Aid and Prizes" (finish first aider); "(finish)" annotation is dropped.
        var nadine = assignments.Single(a => a.Volunteer!.Name == "Nadine Baldwin");
        Assert.Equal("First Aid and Prizes", nadine.VolunteerRole!.Name);
        Assert.Null(nadine.Note);

        // Beth's "(course)" rerouted her to the on-course first-aid role.
        var beth_a = assignments.Single(a => a.Volunteer!.Name == "Beth Gadlin");
        Assert.Equal("First Aid On Course", beth_a.VolunteerRole!.Name);

        // Existing volunteer fields untouched.
        var hiren = db2.Volunteers.Single(v => v.Name == "Hiren Amin");
        Assert.Equal("Male", hiren.Gender);
        Assert.True(hiren.IsFirstAidTrained);

        // New volunteers got the chosen gender + member=true + first-aid=false.
        var beth = db2.Volunteers.Single(v => v.Name == "Beth Gadlin");
        Assert.Equal("Female", beth.Gender);
        Assert.True(beth.IsClubMember);
        Assert.False(beth.IsFirstAidTrained);
    }

    [Fact]
    public async Task Commit_RefusesIfRosterBecameNonEmptyBetweenPreviewAndCommit()
    {
        var preview = _import.BuildPreview(_eventId, BuildSheet(("Lead", "Alice")));

        // Now another assignment sneaks in (e.g. organiser added one in another tab).
        using (var db = _factory.CreateDbContext())
        {
            var role = db.VolunteerRoles.First(r => r.EventType == EventType.CrownToCrown);
            var v = new Volunteer { Name = "Squatter" };
            db.Volunteers.Add(v);
            db.SaveChanges();
            db.VolunteerAssignments.Add(new VolunteerAssignment { EventId = _eventId, VolunteerId = v.Id, VolunteerRoleId = role.Id });
            db.SaveChanges();
        }

        var input = new RosterImportCommitInput
        {
            EventId = _eventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview),
            NewVolunteerGenders = new Dictionary<string, string> { ["alice"] = "Female" }
        };
        var result = await _import.CommitAsync(input);
        Assert.False(result.Success);
    }

    [Fact]
    public async Task Commit_IsIdempotentByName_WhenReImportedAfterClearing()
    {
        // First import.
        var preview1 = _import.BuildPreview(_eventId, BuildSheet(("Lead", "Alice Newby")));
        await _import.CommitAsync(new RosterImportCommitInput
        {
            EventId = _eventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview1),
            NewVolunteerGenders = new Dictionary<string, string> { ["alice newby"] = "Female" }
        });

        // Clear the roster, then re-import.
        using (var db = _factory.CreateDbContext())
        {
            db.VolunteerAssignments.RemoveRange(db.VolunteerAssignments.Where(a => a.EventId == _eventId));
            db.SaveChanges();
        }

        var preview2 = _import.BuildPreview(_eventId, BuildSheet(("Lead", "Alice Newby")));
        Assert.Empty(preview2.NewVolunteers); // already in register from first import
        await _import.CommitAsync(new RosterImportCommitInput
        {
            EventId = _eventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview2),
            NewVolunteerGenders = new()
        });

        using var db2 = _factory.CreateDbContext();
        Assert.Equal(1, db2.Volunteers.Count(v => v.Name == "Alice Newby"));
        Assert.Equal(1, db2.VolunteerAssignments.Count(a => a.EventId == _eventId));
    }

    [Fact]
    public void Preview_FirstAidAndPrizesWithCourse_ReroutesToFirstAidOnCourse()
    {
        var preview = _import.BuildPreview(_eventId, BuildSheet(
            ("First Aid and Prizes", "Nadine Baldwin (finish)\nBeth Gadlin (course)")));

        Assert.Empty(preview.UnmatchedRoles);
        Assert.Equal(2, preview.Assignments.Count);

        var nadine = preview.Assignments.Single(a => a.VolunteerDisplayName == "Nadine Baldwin");
        Assert.Equal("First Aid and Prizes", nadine.RoleName);
        Assert.Null(nadine.Note);

        var beth = preview.Assignments.Single(a => a.VolunteerDisplayName == "Beth Gadlin");
        Assert.Equal("First Aid On Course", beth.RoleName);
        Assert.Null(beth.Note);
    }

    [Fact]
    public void Preview_DoesNotCrashIfRegisterAlreadyHasDuplicateNames()
    {
        using (var db = _factory.CreateDbContext())
        {
            db.Volunteers.Add(new Volunteer { Name = "Michael Gould" });
            db.Volunteers.Add(new Volunteer { Name = "Michael Gould" });
            db.SaveChanges();
        }

        var preview = _import.BuildPreview(_eventId, BuildSheet(("Lead", "Michael Gould")));
        Assert.Empty(preview.Errors);
        Assert.Empty(preview.NewVolunteers);
        Assert.Single(preview.MatchedVolunteers);
    }

    [Fact]
    public void Preview_BluebellEvent_UsesBluebellRoles()
    {
        int bluebellEventId;
        using (var db = _factory.CreateDbContext())
        {
            var ev = new RaceEvent { EventName = "Bluebell 5 2026", EventDate = new DateTime(2026, 5, 1), EventType = EventType.Bluebell5 };
            db.Events.Add(ev);
            db.SaveChanges();
            bluebellEventId = ev.Id;
        }

        var preview = _import.BuildPreview(bluebellEventId, BuildSheet(
            ("Number Pick Up", "Alice"),
            ("Van Driver", "Bob")));
        Assert.Empty(preview.UnmatchedRoles);
        Assert.Equal(2, preview.Assignments.Count);
        Assert.Contains(preview.Assignments, a => a.RoleName == "Number Pick Up");
        Assert.Contains(preview.Assignments, a => a.RoleName == "Van Driver");
    }

    // ----- Helpers -----

    private static Stream BuildSheet(params (string Role, string Volunteers)[] rows)
    {
        var ms = new MemoryStream();
        using (var wb = new XLWorkbook())
        {
            var sheet = wb.Worksheets.Add("Volunteers");
            sheet.Cell(1, 1).Value = "Role";
            sheet.Cell(1, 2).Value = "Volunteer(s)";
            for (int i = 0; i < rows.Length; i++)
            {
                sheet.Cell(i + 2, 1).Value = rows[i].Role;
                sheet.Cell(i + 2, 2).Value = rows[i].Volunteers;
            }
            wb.SaveAs(ms);
        }
        ms.Position = 0;
        return ms;
    }
}
