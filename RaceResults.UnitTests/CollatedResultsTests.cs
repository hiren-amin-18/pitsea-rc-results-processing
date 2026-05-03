using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

public class CollatedResultsTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public async Task GetCollatedResults_OrderedByPosition()
    {
        await SeedFullRace();

        var results = Service.GetCollatedResults();

        Assert.Equal(2, results.Count);
        Assert.Equal(1, results[0].Position);
        Assert.Equal(2, results[1].Position);
    }

    [Fact]
    public async Task GetCollatedResults_TimesAttached()
    {
        await SeedFullRace();

        var results = Service.GetCollatedResults();

        Assert.All(results, r => Assert.False(string.IsNullOrWhiteSpace(r.Time)));
    }

    [Fact]
    public async Task GetCollatedResults_EntrantDetailsAttached()
    {
        await SeedFullRace();

        var results = Service.GetCollatedResults();

        // Position 1 has bib 2 (Bob), position 2 has bib 1 (Alice)
        Assert.Equal("Bob", results[0].Name);
        Assert.Equal("Alice", results[1].Name);
    }

    [Fact]
    public async Task GetCollatedResults_UnmatchedBib_ShowsUnmatchedLabel()
    {
        // Upload entrant with bib 1 only, but finish bib has 999 which is unmatched
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
        ])]);

        // Use an unmatched bib in finish bib (warns but succeeds)
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            ["Position", "Bib"],
            ["1", "999"],
        ]));

        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n"));

        var results = Service.GetCollatedResults();

        Assert.Single(results);
        Assert.Equal("Unmatched bib", results[0].Name);
    }

    [Fact]
    public async Task GetDnfEntrants_ReturnsEntrantsNotInFinishBib()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob", "Club B", "Male", "22"],
            ["3", "Carol", "Club C", "Female", "35"],
        ])]);

        // Only bib 1 and 2 finished
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            ["Position", "Bib"],
            ["1", "1"],
            ["2", "2"],
        ]));

        var dnfs = Service.GetDnfEntrants();

        Assert.Single(dnfs);
        Assert.Equal("3", dnfs[0].BibNumber);
    }

    [Fact]
    public async Task GetDnfEntrants_WhenAllFinished_ReturnsEmpty()
    {
        await SeedFullRace();

        var dnfs = Service.GetDnfEntrants();

        Assert.Empty(dnfs);
    }

    private async Task SeedFullRace()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob", "Club B", "Male", "22"],
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
