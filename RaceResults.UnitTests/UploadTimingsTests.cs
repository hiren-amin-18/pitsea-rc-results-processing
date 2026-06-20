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
    public async Task VirtualVolunteerCsv_WithTwoColumnGunRow_LoadsTimings()
    {
        await SeedEntrantsAndFinishBib();
        // Virtual Volunteer writes the position-0 gun-start row with no duration column.
        var csv = "STARTOFEVENT,10/06/2026 19:31:59,virtual_volunteer_ios_2.3.0_85\n"
            + "0,10/06/2026 19:31:59\n"
            + "1,10/06/2026 19:49:57, 00:17:58\n"
            + "2,10/06/2026 19:50:18, 00:18:19\n"
            + "ENDOFEVENT,10/06/2026 20:16:20\n";
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
    public async Task CsvWithZeroBasedRangeIncludingOne_RemappedToOneBased()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Club A", "Female", "20"],
            ["2", "Bob", "Club B", "Male", "22"],
            ["3", "Cara", "Club C", "Female", "24"],
        ])]);

        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
        [
            FinishHeader,
            ["1", "1"],
            ["2", "2"],
            ["3", "3"],
        ]));

        // CSV is zero-based and includes both 0 and 1 (0,1,2) so remapping must still occur.
        var csv = "STARTOFEVENT,x,x\n0,x,00:20:00\n1,x,00:21:00\n2,x,00:22:00\n";
        var file = FormFileHelpers.CreateCsv("timings.csv", csv);

        var result = await Service.UploadTimingsAsync(file);

        Assert.True(result.Success);
        Assert.Equal(3, Service.GetStatusCounts().TimingCount);

        // Times are normalised to the canonical display format (US17).
        var collated = Service.GetCollatedResults();
        Assert.Equal("20:00", collated.Single(r => r.Position == 1).Time);
        Assert.Equal("21:00", collated.Single(r => r.Position == 2).Time);
        Assert.Equal("22:00", collated.Single(r => r.Position == 3).Time);
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

    [Fact]
    public async Task CsvInvalidPosition_ReturnsFailureWithLineNumber()
    {
        await SeedEntrantsAndFinishBib();
        var file = FormFileHelpers.CreateCsv("timings.csv", "STARTOFEVENT,x,x\nX,x,00:20:00\n");

        var result = await Service.UploadTimingsAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("row 2") && e.Contains("invalid position"));
    }

    [Fact]
    public async Task XlsxMissingRequiredColumn_ReturnsFailureWithLineNumber()
    {
        await SeedEntrantsAndFinishBib();
        var file = FormFileHelpers.CreateXlsx("timings.xlsx",
        [
            ["Position"],
            ["1"],
        ]);

        var result = await Service.UploadTimingsAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("row 1") && e.Contains("missing required column"));
    }

    [Fact]
    public async Task CsvMalformedTime_ReturnsFailureWithRowAndValue()
    {
        await SeedEntrantsAndFinishBib();
        var file = FormFileHelpers.CreateCsv("timings.csv", "STARTOFEVENT,x,x\n1,x,00:2X:99\n2,x,00:21:00\n");

        var result = await Service.UploadTimingsAsync(file);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("row 2") && e.Contains("invalid time") && e.Contains("00:2X:99"));
    }

    [Fact]
    public async Task OutOfOrderTimes_SucceedsWithWarning()
    {
        await SeedEntrantsAndFinishBib();
        // Position 2 finishes faster than position 1 — physically inconsistent with finishing order.
        var file = FormFileHelpers.CreateCsv("timings.csv", "STARTOFEVENT,x,x\n1,x,00:21:00\n2,x,00:20:00\n");

        var result = await Service.UploadTimingsAsync(file);

        Assert.True(result.Success);
        Assert.Contains(result.Warnings, w => w.Contains("not in finishing order") && w.Contains("2"));
    }

    [Fact]
    public async Task ValidTimes_StoreTypedDuration()
    {
        await SeedEntrantsAndFinishBib();
        var file = FormFileHelpers.CreateCsv("timings.csv", "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:21:00\n");

        var result = await Service.UploadTimingsAsync(file);

        Assert.True(result.Success);
        var collated = Service.GetCollatedResults();
        Assert.Equal(new TimeSpan(0, 20, 0), collated.Single(r => r.Position == 1).Duration);
        Assert.Equal(new TimeSpan(0, 21, 0), collated.Single(r => r.Position == 2).Duration);
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
