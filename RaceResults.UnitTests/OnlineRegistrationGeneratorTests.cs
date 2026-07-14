using System.Text;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class OnlineRegistrationGeneratorTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly OnlineRegistrationGenerator _gen;
    private readonly ClubService _clubs;
    private readonly int _c2cEventId;
    private readonly int _bluebellEventId;

    public OnlineRegistrationGeneratorTests()
    {
        (var factory, _connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _clubs = new ClubService(factory);
        _gen = new OnlineRegistrationGenerator(factory, _clubs, NullLogger<OnlineRegistrationGenerator>.Instance);

        using var db = _factory.CreateDbContext();
        // EnsureCreated does honour HasData seeds. Confirm clubs got seeded.
        var c2c = new RaceEvent { EventName = "Crown to Crown July 2026", EventDate = new DateTime(2026, 7, 8), EventType = EventType.CrownToCrown };
        var bluebell = new RaceEvent { EventName = "Bluebell 5 2026", EventDate = new DateTime(2026, 5, 1), EventType = EventType.Bluebell5 };
        db.Events.AddRange(c2c, bluebell);
        db.SaveChanges();
        _c2cEventId = c2c.Id;
        _bluebellEventId = bluebell.Id;
    }

    public void Dispose() => _connection.Dispose();

    // ----- Excel.Proper -----

    [Theory]
    [InlineData("JOHN", "John")]
    [InlineData("john", "John")]
    [InlineData("jOhN sMiTh", "John Smith")]
    [InlineData("o'brien", "O'Brien")]
    [InlineData("mary-jane", "Mary-Jane")]
    [InlineData("  jack  ", "  Jack  ")]
    [InlineData("mcdonald", "Mcdonald")] // Excel PROPER quirk — not overridden per story
    public void Proper_MatchesExcelBehaviour(string input, string expected)
    {
        Assert.Equal(expected, Excel.Proper(input));
    }

    // ----- Club matcher -----

    [Fact]
    public void ClubMatcher_SnapsAbbreviationsToCanonicalNames()
    {
        var canonical = new[]
        {
            new Club { Id = 1, Name = "Pitsea Running Club" },
            new Club { Id = 2, Name = "Rochford Running Club" },
            new Club { Id = 3, Name = "Basildon Athletics Club" },
        };
        Assert.Equal("Pitsea Running Club", ClubMatcher.Match("Pitsea RC", canonical).ConfidentMatch?.Name);
        Assert.Equal("Rochford Running Club", ClubMatcher.Match("rochford rc", canonical).ConfidentMatch?.Name);
        Assert.Equal("Basildon Athletics Club", ClubMatcher.Match("Basildon AC", canonical).ConfidentMatch?.Name);
    }

    [Fact]
    public void ClubMatcher_UnknownClub_ReturnsNoConfidentMatch()
    {
        var canonical = new[] { new Club { Id = 1, Name = "Pitsea Running Club" } };
        var m = ClubMatcher.Match("Some Totally Different Club", canonical);
        Assert.False(m.IsConfident);
    }

    [Fact]
    public void ClubMatcher_MinorTypo_StillMatchesConfidently()
    {
        var canonical = new[] { new Club { Id = 1, Name = "Pitsea Running Club" } };
        var m = ClubMatcher.Match("Pitsea Runnng Club", canonical);
        Assert.True(m.IsConfident);
        Assert.Equal("Pitsea Running Club", m.ConfidentMatch?.Name);
    }

    // ----- Preview / Generation -----

    [Fact]
    public void Preview_RejectsBluebellEvent()
    {
        var preview = _gen.BuildPreview(_bluebellEventId, AdultsCsv(("John", "Smith", "Male", "Pitsea RC")), U18Csv(("Amy", "Jones", "Female", "Pitsea RC", 14)));
        Assert.Contains(preview.Errors, e => e.Contains("Crown to Crown", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_TwoAdultCsvs_Errors()
    {
        var preview = _gen.BuildPreview(_c2cEventId, AdultsCsv(("A","B","Male","Pitsea RC")), AdultsCsv(("C","D","Female","Pitsea RC")));
        Assert.Contains(preview.Errors, e => e.Contains("Neither file", StringComparison.Ordinal));
    }

    [Fact]
    public void Preview_HappyPath_MatchesClubsConfidently()
    {
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(
                ("John", "Smith", "Male", "Pitsea RC"),
                ("Jane", "Doe", "Female", "Basildon AC")),
            U18Csv(("Amy", "Jones", "Female", "Pitsea RC", 14)));

        Assert.Empty(preview.Errors);
        Assert.Empty(preview.UnrecognisedGenders);
        Assert.Empty(preview.UnresolvedClubs);
        Assert.Equal(2, preview.AdultCount);
        Assert.Equal(1, preview.U18Count);
    }

    [Fact]
    public void Preview_UnknownClub_ListedForResolution()
    {
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(("John", "Smith", "Male", "Totally Unknown Club XYZ")),
            U18Csv());
        var unresolved = Assert.Single(preview.UnresolvedClubs);
        Assert.Equal("Totally Unknown Club XYZ", unresolved.Raw);
        Assert.Equal(1, unresolved.OccurrenceCount);
    }

    [Fact]
    public void Preview_DuplicateAcrossFiles_Flagged()
    {
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(("Amy", "Jones", "Female", "Pitsea RC")),
            U18Csv(("Amy", "Jones", "Female", "Pitsea RC", 17)));
        Assert.Single(preview.Duplicates);
    }

    [Fact]
    public void Preview_FilesSwappedByMistake_AutoCorrected()
    {
        // Upload U18 CSV in the "adults" slot and adults CSV in the "U18" slot.
        var preview = _gen.BuildPreview(_c2cEventId,
            adultsCsv: U18Csv(("Amy", "Jones", "Female", "Pitsea RC", 14)),
            u18Csv:    AdultsCsv(("John", "Smith", "Male", "Pitsea RC")));

        Assert.Empty(preview.Errors);
        Assert.Equal(1, preview.AdultCount);
        Assert.Equal(1, preview.U18Count);
    }

    [Fact]
    public void Generate_ProducesXlsxWithAgeColourCodingAndAlphabeticalOrder()
    {
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(
                ("john", "SMITH", "Male", "Pitsea RC"),
                ("Zach", "adams", "Male", "Pitsea Running Club")),
            U18Csv(
                ("amy", "jones", "female", "Pitsea RC", 14),
                ("Ben", "Baker", "MALE", "Pitsea RC", 12)));

        var input = new OnlineRegistrationGenerateInput
        {
            EventId = _c2cEventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview),
        };

        var result = _gen.Generate(input);
        Assert.True(result.Success, string.Join("\n", result.Errors));
        Assert.NotNull(result.Bytes);
        Assert.EndsWith("-online-registration.xlsx", result.FileName);

        using var wb = new XLWorkbook(new MemoryStream(result.Bytes!));
        var sheet = wb.Worksheets.First();

        // Headers
        Assert.Equal("Race #", sheet.Cell(1, 1).GetString());
        Assert.Equal("Name", sheet.Cell(1, 2).GetString());
        Assert.Equal("Club Name", sheet.Cell(1, 3).GetString());
        Assert.Equal("M/F", sheet.Cell(1, 4).GetString());
        Assert.Equal("Age", sheet.Cell(1, 5).GetString());
        Assert.Equal("Comments", sheet.Cell(1, 6).GetString());

        // Rows: expected alphabetical by Surname then Forename: Adams Zach, Baker Ben, Jones Amy, Smith John.
        Assert.Equal("Zach Adams", sheet.Cell(2, 2).GetString());
        Assert.Equal("Ben Baker", sheet.Cell(3, 2).GetString());
        Assert.Equal("Amy Jones", sheet.Cell(4, 2).GetString());
        Assert.Equal("John Smith", sheet.Cell(5, 2).GetString());

        // Race # is blank on every data row (AC4)
        for (int r = 2; r <= 5; r++)
            Assert.True(sheet.Cell(r, 1).IsEmpty(), $"Row {r} Race # should be blank");

        // Adults have blank Age with no fill; U18s have age and coloured fill.
        Assert.True(sheet.Cell(2, 5).IsEmpty()); // Adam Zach adult
        Assert.True(sheet.Cell(5, 5).IsEmpty()); // John Smith adult

        // Ben Baker (Male U18, row 3): Age 12, blue fill
        Assert.Equal(12, sheet.Cell(3, 5).GetValue<int>());
        Assert.Equal("dce6f2", sheet.Cell(3, 5).Style.Fill.BackgroundColor.Color.Name[^6..].ToLowerInvariant());

        // Amy Jones (Female U18, row 4): Age 14, pink fill
        Assert.Equal(14, sheet.Cell(4, 5).GetValue<int>());
        Assert.Equal("f2dcdb", sheet.Cell(4, 5).Style.Fill.BackgroundColor.Color.Name[^6..].ToLowerInvariant());
    }

    [Fact]
    public void Generate_AddNewClub_PersistsToClubsTable()
    {
        var newClubName = "Wildly Named Running Society";
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(("John", "Smith", "Male", newClubName)),
            U18Csv());

        var input = new OnlineRegistrationGenerateInput
        {
            EventId = _c2cEventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview),
            ClubResolutions = new Dictionary<string, string>
            {
                [newClubName] = $"add:{newClubName}"
            }
        };

        var result = _gen.Generate(input);
        Assert.True(result.Success);

        var clubs = _clubs.GetClubs();
        Assert.Contains(clubs, c => c.Name == newClubName);

        // Re-running the preview should now confidently match (AC10).
        var second = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(("John", "Smith", "Male", newClubName)),
            U18Csv());
        Assert.Empty(second.UnresolvedClubs);
    }

    [Fact]
    public void Generate_LeaveAsTyped_UsesRawClubInOutput()
    {
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(("John", "Smith", "Male", "Weird Unknown Club")),
            U18Csv());

        var input = new OnlineRegistrationGenerateInput
        {
            EventId = _c2cEventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview),
            ClubResolutions = new Dictionary<string, string>
            {
                ["Weird Unknown Club"] = "leave"
            }
        };

        var result = _gen.Generate(input);
        Assert.True(result.Success);

        using var wb = new XLWorkbook(new MemoryStream(result.Bytes!));
        Assert.Equal("Weird Unknown Club", wb.Worksheets.First().Cell(2, 3).GetString());
    }

    [Fact]
    public void Generate_MissingResolution_Fails()
    {
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(("John", "Smith", "Male", "Uncertain Club")),
            U18Csv());

        var input = new OnlineRegistrationGenerateInput
        {
            EventId = _c2cEventId,
            PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview),
            // No club resolution supplied.
        };

        var result = _gen.Generate(input);
        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("No resolution", StringComparison.Ordinal));
    }

    [Fact]
    public void Generate_FilenameFollowsEventSlugPattern()
    {
        var preview = _gen.BuildPreview(_c2cEventId,
            AdultsCsv(("A","B","Male","Pitsea RC")),
            U18Csv());
        var input = new OnlineRegistrationGenerateInput { EventId = _c2cEventId, PreviewJson = System.Text.Json.JsonSerializer.Serialize(preview) };
        var result = _gen.Generate(input);
        Assert.True(result.Success);
        Assert.Equal("crown-to-crown-july-2026-2026-07-08-online-registration.xlsx", result.FileName);
    }

    // ----- Helpers -----

    private static Stream AdultsCsv(params (string Forename, string Surname, string Gender, string Club)[] rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Forename,Surname,Gender,Club");
        foreach (var r in rows)
        {
            sb.AppendLine($"{Csv(r.Forename)},{Csv(r.Surname)},{Csv(r.Gender)},{Csv(r.Club)}");
        }
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static Stream U18Csv(params (string Forename, string Surname, string Gender, string Club, int Age)[] rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Forename,Surname,Gender,Club,Age_on_Race_Day,U18IsDependantOfUser");
        foreach (var r in rows)
        {
            sb.AppendLine($"{Csv(r.Forename)},{Csv(r.Surname)},{Csv(r.Gender)},{Csv(r.Club)},{r.Age},true");
        }
        return new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
    }

    private static string Csv(string s) => s.Contains(',') ? $"\"{s}\"" : s;
}
