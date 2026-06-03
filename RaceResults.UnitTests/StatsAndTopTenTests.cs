using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

public class StatsAndTopTenTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public async Task GetRaceStats_CountsCorrectly()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice",   "Club A", "Female", "20"],  // Female, adult, affiliated
            ["2", "Bob",     "",       "Male",   "15"],  // Male,   U18,   unaffiliated
            ["3", "Carol",   "",       "Female", "16"],  // Female, U18,   unaffiliated
            ["4", "Dave",    "",       "Male",   "30"],  // Male,   adult, unaffiliated
            ["5", "Eve",     "Club B", "Female", "40"],  // Female, adult, affiliated
            ["6", "Frank",   "Club C", "Male",   "22"],  // Male,   adult, affiliated
        ])]);

        var stats = Service.GetRaceStats();

        Assert.Equal(3, stats.TotalMales);
        Assert.Equal(3, stats.TotalFemales);
        Assert.Equal(1, stats.TotalMalesU18);
        Assert.Equal(1, stats.TotalFemalesU18);
        Assert.Equal(1, stats.TotalMalesUnaffiliatedExcludingU18);   // Dave
        Assert.Equal(0, stats.TotalFemalesUnaffiliatedExcludingU18); // Carol is U18, excluded
    }

    [Fact]
    public async Task GetTopTenByCategory_ReturnsFourCategories()
    {
        await SeedMinimalRace();

        var categories = Service.GetTopTenByCategory();

        Assert.Equal(4, categories.Count);
        Assert.Contains(categories, c => c.Name == "Male");
        Assert.Contains(categories, c => c.Name == "Female");
        Assert.Contains(categories, c => c.Name == "Male U18");
        Assert.Contains(categories, c => c.Name == "Female U18");
    }

    [Fact]
    public async Task GetTopTenByCategory_MaleCategory_OnlyMales()
    {
        await SeedMinimalRace();

        var categories = Service.GetTopTenByCategory();
        var males = categories.First(c => c.Name == "Male");

        Assert.All(males.Results, r => Assert.Equal("Male", r.Gender));
        Assert.All(males.Results, r => Assert.False(r.IsU18, "Male category should exclude U18 runners"));
    }

    [Fact]
    public async Task GetTopTenByCategory_LimitedToTen()
    {
        // Seed 15 male runners
        var rows = new List<string[]> { EntrantHeader };
        for (int i = 1; i <= 15; i++)
        {
            rows.Add([$"{i}", $"Runner{i}", "Club", "Male", "25"]);
        }

        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx", rows)]);

        var finishRows = new List<string[]> { new[] { "Position", "Bib" } };
        for (int i = 1; i <= 15; i++) finishRows.Add([$"{i}", $"{i}"]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", finishRows));

        var csvLines = "STARTOFEVENT,x,x\n" +
            string.Join("\n", Enumerable.Range(1, 15).Select(i => $"{i},x,00:{20 + i:D2}:00"));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", csvLines));

        var categories = Service.GetTopTenByCategory();
        var males = categories.First(c => c.Name == "Male");

        Assert.Equal(10, males.Results.Count);
    }

    private async Task SeedMinimalRace()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice",  "Club A", "Female", "20"],
            ["2", "Bob",    "Club B", "Male",   "22"],
            ["3", "Charlie","Club C", "Male",   "16"],
            ["4", "Dana",   "Club D", "Female", "15"],
        ])]);

        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            ["Position", "Bib"],
            ["1", "2"],
            ["2", "3"],
            ["3", "1"],
            ["4", "4"],
        ]));

        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n3,x,00:22:00\n4,x,00:23:00\n"));
    }
}
