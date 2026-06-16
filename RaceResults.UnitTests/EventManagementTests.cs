namespace RaceResults.UnitTests;

public class EventManagementTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public void GetCurrentEvent_DefaultEventExists()
    {
        var current = Service.GetCurrentEvent();

        Assert.NotNull(current);
        Assert.True(current.IsCurrent);
        Assert.False(string.IsNullOrWhiteSpace(current.EventName));
    }

    [Fact]
    public void CreateEvent_SetsAsCurrent()
    {
        var result = Service.CreateEvent(new Web.Models.CreateEventInput
        {
            EventName = "Bluebell 5 2026",
            EventDate = new DateTime(2026, 5, 1),
            EventType = Web.Models.EventType.Bluebell5
        });

        Assert.True(result.Success);
        var current = Service.GetCurrentEvent();
        Assert.Equal("Bluebell 5 2026", current.EventName);
        Assert.Equal(Web.Models.EventType.Bluebell5, current.EventType);
    }

    [Fact]
    public void DeleteEvent_OnLastEvent_LeavesDbEmpty()
    {
        // Delete the seeded default event so nothing remains.
        var only = Service.GetCurrentEvent();
        Assert.NotNull(only);

        var deleteResult = Service.DeleteEvent(only!.Id);

        Assert.True(deleteResult.Success);
        Assert.Empty(Service.GetEvents());
        Assert.Null(Service.GetCurrentEvent());
    }

    [Fact]
    public void DeleteEvent_RemovesItAndKeepsCurrentEvent()
    {
        Service.CreateEvent(new Web.Models.CreateEventInput
        {
            EventName = "Temp Event",
            EventDate = new DateTime(2026, 6, 1),
            EventType = Web.Models.EventType.CrownToCrown
        });

        var toDelete = Service.GetCurrentEvent();
        var deleteResult = Service.DeleteEvent(toDelete.Id);

        Assert.True(deleteResult.Success);
        var current = Service.GetCurrentEvent();
        Assert.NotEqual(toDelete.Id, current.Id);

        var allEvents = Service.GetEvents();
        Assert.DoesNotContain(allEvents, e => e.Id == toDelete.Id);
    }

    [Fact]
    public async Task StatusCounts_AreScopedToCurrentEvent()
    {
        await Service.UploadEntrantsAsync([Helpers.FormFileHelpers.CreateXlsx("e1.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "30"],
        ])]);

        var firstEvent = Service.GetCurrentEvent();
        Assert.Equal(1, Service.GetStatusCounts().EntrantCount);

        Service.CreateEvent(new Web.Models.CreateEventInput
        {
            EventName = "Bluebell 5 2026",
            EventDate = new DateTime(2026, 5, 1),
            EventType = Web.Models.EventType.Bluebell5
        });

        Assert.Equal(0, Service.GetStatusCounts().EntrantCount);

        await Service.UploadEntrantsAsync([Helpers.FormFileHelpers.CreateXlsx("e2.xlsx",
        [
            EntrantHeader,
            ["2", "Bob", "Club B", "Male", "31"],
        ])]);

        Assert.Equal(1, Service.GetStatusCounts().EntrantCount);

        Service.SetCurrentEvent(firstEvent.Id);
        var firstEventCounts = Service.GetStatusCounts();
        Assert.Equal(1, firstEventCounts.EntrantCount);
        Assert.Equal(0, firstEventCounts.FinishBibCount);
        Assert.Equal(0, firstEventCounts.TimingCount);
    }

    [Fact]
    public async Task DeleteEvent_OnlyRemovesDataForDeletedEvent()
    {
        await Service.UploadEntrantsAsync([Helpers.FormFileHelpers.CreateXlsx("e1.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "30"],
        ])]);

        var firstEvent = Service.GetCurrentEvent();

        Service.CreateEvent(new Web.Models.CreateEventInput
        {
            EventName = "Bluebell 5 2026",
            EventDate = new DateTime(2026, 5, 1),
            EventType = Web.Models.EventType.Bluebell5
        });

        await Service.UploadEntrantsAsync([Helpers.FormFileHelpers.CreateXlsx("e2.xlsx",
        [
            EntrantHeader,
            ["2", "Bob", "Club B", "Male", "31"],
        ])]);

        var secondEvent = Service.GetCurrentEvent();
        var delete = Service.DeleteEvent(secondEvent.Id);
        Assert.True(delete.Success);

        Service.SetCurrentEvent(firstEvent.Id);
        var remainingCounts = Service.GetStatusCounts();
        Assert.Equal(1, remainingCounts.EntrantCount);
    }
}
