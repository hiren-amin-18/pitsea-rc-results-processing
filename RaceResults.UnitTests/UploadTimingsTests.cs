using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

public class UploadTimingsTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];
    private static readonly string[] FinishHeader = ["Position", "Bib"];

    [Fact]
    public async Task NullFile_ReturnsFailure()
    {
        var result = await Service.UploadTimingsAsync(null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task NoFinishBibUploaded_ReturnsFailure()
    {
        var file = FormFileHelpers.CreateCsv("t.csv", "1,x,00:20:00\n");

        var result = await Service.UploadTimingsAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Upload finish position"));
    }

    [Fact]
    public async Task UnsupportedFileType_ReturnsFailure()
    {
        await SeedEntrantsAndFinishBib();
        var file = FormFileHelpers.CreateCsv("timings.txt", "data");

        var result = await Service.UploadTimingsAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains(".csv or .xlsx"));
    }

    [Fact]
    public async Task ValidCsvWithStartOfEvent_LoadsTimings()
    {
        await SeedEntrantsAndFinishBib();
        var csv = "STARTOFEVENT,03/04/2026 11:18:54,x\n1,x,00:20:00\n2,x,00:21:00\n";
        var file = FormFileHelpers.CreateCsv("timings.csv", csv);

        var result = await Service.UploadTimingsAsync(file);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().TimingCount);
    }

    [Fact]
    public async Task CsvWithZeroBasedPositions_RemappedToOneBased()
    {
        // Seed with only 1 finish bib row so single position maps cleanly
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
        ])]);

        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            FinishHeader,
            ["1", "1"],
        ]));

        // CSV has position 0 only (no position 1) — should remap 0 → 1
        var csv = "STARTOFEVENT,x,x\n0,x,00:20:00\n";
        var file = FormFileHelpers.CreateCsv("timings.csv", csv);

        var result = await Service.UploadTimingsAsync(file);

        Assert.True(result.Success);
        Assert.Equal(1, Service.GetStatusCounts().TimingCount);
    }

    [Fact]
    public async Task ValidXlsx_LoadsTimings()
    {
        await SeedEntrantsAndFinishBib();
        var file = FormFileHelpers.CreateXlsx("timings.xlsx",
        [
            ["Position", "Time"],
            ["1", "00:20:00"],
            ["2", "00:21:00"],
        ]);

        var result = await Service.UploadTimingsAsync(file);

        Assert.True(result.Success);
        Assert.Equal(2, Service.GetStatusCounts().TimingCount);
    }

    [Fact]
    public async Task MissingTimingPositions_ReturnsFailure()
    {
        await SeedEntrantsAndFinishBib();
        // Only provides position 1, missing position 2
        var csv = "STARTOFEVENT,x,x\n1,x,00:20:00\n";
        var file = FormFileHelpers.CreateCsv("timings.csv", csv);

        var result = await Service.UploadTimingsAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("missing positions"));
    }

    [Fact]
    public async Task ExtraTimingPositions_ReturnsFailure()
    {
        await SeedEntrantsAndFinishBib();
        // Provides positions 1, 2, 99 — 99 is extra
        var csv = "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n99,x,00:30:00\n";
        var file = FormFileHelpers.CreateCsv("timings.csv", csv);

        var result = await Service.UploadTimingsAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("unexpected positions"));
    }

    private async Task SeedEntrantsAndFinishBib()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob", "Club B", "Male", "22"],
        ])]);

        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            FinishHeader,
            ["1", "2"],
            ["2", "1"],
        ]));
    }
}
