using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

public class CourseRecordTests : IDisposable
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    private readonly SqliteConnection _connection;
    private readonly IDbContextFactory<RaceResultsDbContext> _factory;
    private readonly RaceResultsService _raceService;
    private readonly CourseRecordService _records;

    public CourseRecordTests()
    {
        var (factory, connection) = DbContextHelpers.CreateFactory();
        _factory = factory;
        _connection = connection;
        _raceService = new RaceResultsService(factory, NullLogger<RaceResultsService>.Instance);
        _records = new CourseRecordService(factory, _raceService, NullLogger<CourseRecordService>.Instance);
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void Seed_CrownToCrownRecordsExist_BluebellEmpty()
    {
        using var db = _factory.CreateDbContext();
        Assert.Equal(2, db.CourseRecords.Count(r => r.EventType == EventType.CrownToCrown && r.IsCurrent));
        Assert.Equal(0, db.CourseRecords.Count(r => r.EventType == EventType.Bluebell5));
        Assert.Contains(db.CourseRecords, r => r.RunnerName == "Adam Hickey");
    }

    [Fact]
    public async Task GetPendingRecords_FlagsWinnerFasterThanRecord()
    {
        // Default seeded event is Crown to Crown; a 14:00 male winner beats the seeded 15:25 record.
        await SeedRace(("1", "Speedy", "Club", "Male"), time1: "00:14:00");

        var pending = _records.GetPendingRecords(_raceService.GetCurrentEvent().Id);

        var male = Assert.Single(pending, p => p.Category == "Male");
        Assert.Equal("14:00", male.Time);
        Assert.Equal("15:25", male.PreviousTime);
    }

    [Fact]
    public async Task GetPendingRecords_SlowerThanRecord_NotFlagged()
    {
        await SeedRace(("1", "Steady", "Club", "Male"), time1: "00:20:00");

        var pending = _records.GetPendingRecords(_raceService.GetCurrentEvent().Id);

        Assert.DoesNotContain(pending, p => p.Category == "Male");
    }

    [Fact]
    public async Task ConfirmRecord_SupersedesOldRecordAndKeepsHistory()
    {
        await SeedRace(("1", "Speedy", "Club", "Male"), time1: "00:14:00");
        var eventId = _raceService.GetCurrentEvent().Id;

        var result = _records.ConfirmRecord(eventId, "Male");

        Assert.True(result.Success);
        using var db = _factory.CreateDbContext();
        var current = db.CourseRecords.Single(r => r.EventType == EventType.CrownToCrown && r.Category == "Male" && r.IsCurrent);
        Assert.Equal("Speedy", current.RunnerName);
        Assert.Equal(eventId, current.SourceEventId);
        // The old record is retained as history.
        Assert.Contains(db.CourseRecords, r => r.RunnerName == "Adam Hickey" && !r.IsCurrent);
    }

    [Fact]
    public void UpsertRecord_SetsFirstRecordForEmptyCategory()
    {
        var result = _records.UpsertRecord(new EditCourseRecordInput
        {
            EventType = EventType.Bluebell5,
            Category = "Female",
            Time = "19:30",
            RunnerName = "New Holder",
            Club = "Club X",
            EventName = "Bluebell 5",
            EventDate = new DateTime(2025, 5, 1)
        });

        Assert.True(result.Success);
        var slot = _records.GetCurrentRecordSlots()
            .Single(s => s.EventType == EventType.Bluebell5 && s.Category == "Female");
        Assert.NotNull(slot.Current);
        Assert.Equal("New Holder", slot.Current!.RunnerName);
    }

    private async Task SeedRace((string Bib, string Name, string Club, string Gender) winner, string time1)
    {
        await _raceService.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            [winner.Bib, winner.Name, winner.Club, winner.Gender, ""],
            ["2", "Second", "Club", "Male", ""],
        ])]);
        await _raceService.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
            [["Position", "Bib"], ["1", winner.Bib], ["2", "2"]]));
        await _raceService.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            $"STARTOFEVENT,x,x\n1,x,{time1}\n2,x,00:25:00\n"));
    }
}
