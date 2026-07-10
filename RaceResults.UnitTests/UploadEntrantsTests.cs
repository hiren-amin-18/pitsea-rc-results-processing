using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

public class UploadEntrantsTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public async Task NoFiles_ReturnsFailure()
    {
        var result = await Service.UploadEntrantsAsync([]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Upload at least one"));
    }

    [Fact]
    public async Task NonXlsxFile_ReturnsFailure()
    {
        var file = FormFileHelpers.CreateCsv("entrants.csv", "Bib,Name\n1,Alice");

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains(".xlsx"));
    }

    [Fact]
    public async Task ValidFile_LoadsEntrants()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["1", "Alice Smith", "Club A", "Female", "30"],
            ["2", "Bob Jones", "Club B", "Male", "25"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success);
        var counts = Service.GetStatusCounts();
        Assert.Equal(2, counts.EntrantCount);
    }

    [Fact]
    public async Task DuplicateBibsInFile_ReturnsFailure()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["1", "Alice Smith", "Club A", "Female", "30"],
            ["1", "Bob Jones", "Club B", "Male", "25"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Duplicate bib"));
    }

    [Fact]
    public async Task MissingRequiredColumn_ReturnsFailure()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            ["Name", "Club", "Gender", "Age"],  // missing Bib
            ["Alice Smith", "Club A", "Female", "30"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("row 1") && e.Contains("missing required column"));
    }

    [Fact]
    public async Task InvalidAge_ReturnsFailure()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["1", "Alice Smith", "Club A", "Female", "notanumber"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("age is invalid"));
    }

    [Fact]
    public async Task NoAgeColumn_SucceedsWithNullAge()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            ["Bib", "Name", "Club", "Gender"],
            ["1", "Alice Smith", "Club A", "Female"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success);
        Assert.Equal(1, Service.GetStatusCounts().EntrantCount);
    }

    [Fact]
    public async Task BlankAgeCell_SucceedsWithNullAge()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["1", "Alice Smith", "Club A", "Female", ""],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success);
        Assert.Equal(1, Service.GetStatusCounts().EntrantCount);
    }

    [Fact]
    public async Task BibOnlyEntrant_BlankNameAndGender_IsLoaded()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["214", "", "", "", ""],  // only the bib is present
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success);
        Assert.Equal(1, Service.GetStatusCounts().EntrantCount);
    }

    [Fact]
    public async Task BlankBib_StillRejected()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["", "Alice Smith", "Club A", "Female", "30"],  // no bib
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("bib is required"));
    }

    [Fact]
    public async Task BlankNameEntrant_ShownAsUnknownInResults()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["214", "", "", "", ""],  // bib-only entrant
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            ["Position", "Bib"],
            ["1", "214"],
        ]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n"));

        var results = Service.GetCollatedResults();

        Assert.Single(results);
        Assert.Equal("Unknown", results[0].Name);
    }

    [Fact]
    public async Task BlankGenderEntrant_ExcludedFromGenderCounts()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["214", "", "", "", ""],  // bib-only entrant, no gender
        ]);

        await Service.UploadEntrantsAsync([file]);

        var stats = Service.GetRaceStats();
        Assert.Equal(0, stats.TotalMales);
        Assert.Equal(0, stats.TotalFemales);
    }

    [Fact]
    public async Task UploadingNewEntrants_ClearsExistingFinishBibAndTimingData()
    {
        // Arrange: seed all three tables via the full upload flow
        await SeedFullRace();
        var beforeCounts = Service.GetStatusCounts();
        Assert.True(beforeCounts.FinishBibCount > 0);
        Assert.True(beforeCounts.TimingCount > 0);

        // Act: re-upload entrants only
        var newEntrants = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            EntrantHeader,
            ["99", "New Runner", "Club Z", "Male", "40"],
        ]);
        await Service.UploadEntrantsAsync([newEntrants]);

        // Assert finish bib and timing cleared
        var afterCounts = Service.GetStatusCounts();
        Assert.Equal(0, afterCounts.FinishBibCount);
        Assert.Equal(0, afterCounts.TimingCount);
        Assert.Equal(1, afterCounts.EntrantCount);
    }

    [Fact]
    public async Task MultipleFiles_AllEntrantsLoaded()
    {
        var file1 = FormFileHelpers.CreateXlsx("file1.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
        ]);
        var file2 = FormFileHelpers.CreateXlsx("file2.xlsx",
        [
            EntrantHeader,
            ["2", "Bob", "Club B", "Male", "22"],
        ]);

        var result = await Service.UploadEntrantsAsync([file1, file2]);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().EntrantCount);
    }

    [Fact]
    public async Task RaceNumberHeader_AcceptedAsBibAlias()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            ["Race Number", "Name", "Club Name", "M/F", "Age"],
            ["1", "Alice Smith", "Club A", "F", "30"],
            ["2", "Bob Jones", "Club B", "M", "25"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().EntrantCount);
    }

    [Fact]
    public async Task RaceNoHeader_AcceptedAsBibAlias()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            ["Race No", "Name", "Club", "Gender"],
            ["10", "Alice Smith", "Club A", "Female"],
            ["11", "Bob Jones", "Club B", "Male"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().EntrantCount);
    }

    [Fact]
    public async Task ClubNameHeader_AcceptedAsClubAlias()
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            ["Bib", "Name", "Club Name", "Gender", "Age"],
            ["1", "Alice Smith", "Pitsea RC", "Female", "30"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success);
        Assert.Equal(1, Service.GetStatusCounts().EntrantCount);
    }

    [Theory]
    [InlineData("M", "Male")]
    [InlineData("F", "Female")]
    [InlineData("m", "Male")]
    [InlineData("f", "Female")]
    [InlineData("Male", "Male")]
    [InlineData("Female", "Female")]
    public async Task GenderValues_NormalisedCorrectly(string inputGender, string expectedGender)
    {
        var file = FormFileHelpers.CreateXlsx("entrants.xlsx",
        [
            ["Bib", "Name", "Club", "M/F", "Age"],
            ["1", "Runner One", "Club A", inputGender, "25"],
        ]);

        await Service.UploadEntrantsAsync([file]);

        var stats = Service.GetRaceStats();
        if (expectedGender == "Male")
            Assert.Equal(1, stats.TotalMales);
        else
            Assert.Equal(1, stats.TotalFemales);
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
