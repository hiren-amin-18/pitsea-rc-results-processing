using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

public class EditResultTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public async Task TryGetEditableResult_NotFound_ReturnsFalse()
    {
        var found = Service.TryGetEditableResult(999, out _);

        Assert.False(found);
    }

    [Fact]
    public async Task TryGetEditableResult_Found_ReturnsTrueWithData()
    {
        await SeedFullRace();

        var found = Service.TryGetEditableResult(1, out var editInput);

        Assert.True(found);
        Assert.Equal(1, editInput.OriginalPosition);
        Assert.Equal("2", editInput.BibNumber);
        Assert.Equal("00:20:00", editInput.Time);
    }

    [Fact]
    public async Task UpdateResult_InvalidBib_ReturnsFailure()
    {
        await SeedFullRace();

        var result = Service.UpdateResult(new Web.Models.EditResultInput
        {
            OriginalPosition = 1,
            NewPosition = 1,
            BibNumber = "999",   // doesn't exist
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Bib number"));
    }

    [Fact]
    public async Task UpdateResult_DuplicatePosition_ReturnsFailure()
    {
        await SeedFullRace();

        var result = Service.UpdateResult(new Web.Models.EditResultInput
        {
            OriginalPosition = 1,
            NewPosition = 2,    // already taken
            BibNumber = "2",
        });

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("new position is already used"));
    }

    [Fact]
    public async Task UpdateResult_ChangeBib_Persists()
    {
        // Seed a race where position 1 has bib 1 (no bib conflict when we update time)
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob",   "Club B", "Male",   "22"],
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            ["Position", "Bib"],
            ["1", "1"],
            ["2", "2"],
        ]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n"));

        // Update the time on position 1 (bib unchanged — no unique violation)
        var result = Service.UpdateResult(new Web.Models.EditResultInput
        {
            OriginalPosition = 1,
            NewPosition = 1,
            BibNumber = "1",
            Time = "00:19:30",
        });

        Assert.True(result.Success);
        Service.TryGetEditableResult(1, out var updated);
        Assert.Equal("1", updated.BibNumber);
        Assert.Equal("00:19:30", updated.Time);
    }

    [Fact]
    public async Task UpdateResult_ChangePosition_TimingMovesWithIt()
    {
        await SeedFullRace();

        // Move position 1 to position 3
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob",   "Club B", "Male",   "22"],
            ["3", "Carol", "Club C", "Female", "30"],
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            ["Position", "Bib"],
            ["1", "1"],
            ["2", "2"],
        ]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n"));

        var result = Service.UpdateResult(new Web.Models.EditResultInput
        {
            OriginalPosition = 1,
            NewPosition = 3,
            BibNumber = "1",
        });

        Assert.True(result.Success);
        Service.TryGetEditableResult(3, out var moved);
        Assert.Equal("1", moved.BibNumber);
        Assert.Equal("00:20:00", moved.Time);
    }

    private async Task SeedFullRace()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob",   "Club B", "Male",   "22"],
        ])]);

        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            ["Position", "Bib"],
            ["1", "2"],
            ["2", "1"],
        ]));

        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n"));
    }
}
