using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

public class UploadFinishBibTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];
    private static readonly string[] FinishHeader = ["Position", "Bib"];

    [Fact]
    public async Task NullFile_ReturnsFailure()
    {
        var result = await Service.UploadFinishBibAsync(null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task NonXlsxFile_ReturnsFailure()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateCsv("finish.csv", "Position,Bib\n1,1");

        var result = await Service.UploadFinishBibAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains(".xlsx"));
    }

    [Fact]
    public async Task NoEntrantsUploaded_ReturnsFailure()
    {
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            FinishHeader,
            ["1", "1"],
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Upload entrants"));
    }

    [Fact]
    public async Task ValidFile_LoadsFinishBibRows()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            FinishHeader,
            ["1", "1"],
            ["2", "2"],
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().FinishBibCount);
    }

    [Fact]
    public async Task DuplicatePositions_ReturnsFailure()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            FinishHeader,
            ["1", "1"],
            ["1", "2"],
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("row 3") && e.Contains("duplicate position"));
    }

    [Fact]
    public async Task DuplicateBibs_ReturnsFailure()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            FinishHeader,
            ["1", "1"],
            ["2", "1"],
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("row 3") && e.Contains("duplicate bib"));
    }

    [Fact]
    public async Task MissingRequiredColumn_ReturnsFailureWithLineNumber()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            ["Place"],
            ["1"],
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("row 1") && e.Contains("missing required column"));
    }

    [Fact]
    public async Task UnmatchedBibs_ReturnsSuccessWithWarning()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            FinishHeader,
            ["1", "999"],  // bib 999 not in entrants
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("Unmatched bib"));
    }

    [Fact]
    public async Task Upload_ClearsTimingData()
    {
        await SeedEntrantsAndFinishBib();
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n"));
        Assert.Equal(2, Service.GetStatusCounts().TimingCount);

        // Re-upload finish bib
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            FinishHeader,
            ["1", "1"],
            ["2", "2"],
        ]);
        await Service.UploadFinishBibAsync(file);

        Assert.Equal(0, Service.GetStatusCounts().TimingCount);
    }

    [Fact]
    public async Task RaceNumberHeader_AcceptedAsBibAlias()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            ["Position", "Race Number"],
            ["1", "1"],
            ["2", "2"],
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().FinishBibCount);
    }

    [Fact]
    public async Task RaceNoHeader_AcceptedAsBibAlias()
    {
        await SeedEntrants();
        var file = FormFileHelpers.CreateXlsx("finish.xlsx",
        [
            ["Position", "Race No"],
            ["1", "1"],
            ["2", "2"],
        ]);

        var result = await Service.UploadFinishBibAsync(file);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().FinishBibCount);
    }

    private async Task SeedEntrants()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob", "Club B", "Male", "22"],
        ])]);
    }

    private async Task SeedEntrantsAndFinishBib()
    {
        await SeedEntrants();
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            FinishHeader,
            ["1", "2"],
            ["2", "1"],
        ]));
    }
}
