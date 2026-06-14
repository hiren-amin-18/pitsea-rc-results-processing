using QuestPDF.Infrastructure;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Models;

namespace RaceResults.UnitTests;

public class EventArchivingTests : RaceResultsServiceTestBase
{
    static EventArchivingTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public void ArchivedEvent_CannotBeSetCurrent()
    {
        var firstId = Service.GetCurrentEvent().Id;
        Service.CreateEvent(new CreateEventInput { EventName = "Other", EventDate = new DateTime(2026, 6, 1), EventType = EventType.CrownToCrown });
        Service.SetCurrentEvent(firstId);

        Service.ArchiveEvent(firstId);

        // Archiving the current event promoted the other event; the archived one cannot be re-selected.
        Assert.NotEqual(firstId, Service.GetCurrentEvent().Id);
        Assert.False(Service.SetCurrentEvent(firstId).Success);
    }

    [Fact]
    public async Task ArchivedCurrentEvent_PromotesAnotherAndBlocksMutations()
    {
        await SeedRace();
        var archivedId = Service.GetCurrentEvent().Id;
        Service.CreateEvent(new CreateEventInput { EventName = "Next", EventDate = new DateTime(2026, 8, 1), EventType = EventType.CrownToCrown });
        Service.SetCurrentEvent(archivedId);

        var result = Service.ArchiveEvent(archivedId);

        Assert.True(result.Success);
        var current = Service.GetCurrentEvent();
        Assert.NotEqual(archivedId, current.Id);
        Assert.False(current.IsArchived);

        // Editing event detail of an archived event is rejected.
        var edit = Service.UpdateEvent(new EditEventInput { Id = archivedId, EventName = "x", EventDate = new DateTime(2026, 4, 3), EventType = EventType.CrownToCrown });
        Assert.False(edit.Success);
        Assert.Contains(edit.Errors, e => e.Contains("archived"));

        // Deleting an archived event is rejected until unarchived.
        var del = Service.DeleteEvent(archivedId);
        Assert.False(del.Success);
    }

    [Fact]
    public async Task ArchivedEvent_RemainsViewableByEventId()
    {
        await SeedRace();
        var eventId = Service.GetCurrentEvent().Id;
        Service.CreateEvent(new CreateEventInput { EventName = "Next", EventDate = new DateTime(2026, 8, 1), EventType = EventType.CrownToCrown });
        Service.SetCurrentEvent(eventId);
        Service.ArchiveEvent(eventId);

        // Viewing by id still works (read-only).
        Assert.Equal(2, Service.GetCollatedResults(eventId).Count);
        var pdf = Service.GenerateResultsPdf(eventId);
        Assert.Equal(0x25, pdf[0]); // %PDF
    }

    [Fact]
    public async Task Unarchive_RestoresEditing()
    {
        await SeedRace();
        var eventId = Service.GetCurrentEvent().Id;
        Service.CreateEvent(new CreateEventInput { EventName = "Next", EventDate = new DateTime(2026, 8, 1), EventType = EventType.CrownToCrown });
        Service.SetCurrentEvent(eventId);
        Service.ArchiveEvent(eventId);

        var unarchive = Service.UnarchiveEvent(eventId);
        Assert.True(unarchive.Success);

        // Now it can be made current again and edited.
        Assert.True(Service.SetCurrentEvent(eventId).Success);
        Assert.True(Service.DisqualifyResult(1, "test").Success);
    }

    private async Task SeedRace()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", ""],
            ["2", "Bob", "Club B", "Male", ""],
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", [["Position", "Bib"], ["1", "1"], ["2", "2"]]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n"));
    }
}
