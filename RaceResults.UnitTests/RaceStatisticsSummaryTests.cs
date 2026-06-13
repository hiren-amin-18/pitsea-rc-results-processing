using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Models;

namespace RaceResults.UnitTests;

public class RaceStatisticsSummaryTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public void EmptyRace_ReturnsZeroedSummary()
    {
        var summary = Service.GetRaceStatisticsSummary();

        Assert.Equal(0, summary.EntrantCount);
        Assert.Equal(0, summary.FinisherCount);
        Assert.Equal(0, summary.CompletionRatePercent);
        Assert.False(summary.HasTimes);
        Assert.Null(summary.BusiestWindowMinute);
    }

    [Fact]
    public async Task CompletionRate_CountsFinishersOverStarters()
    {
        // 4 entrants, 3 finish, 1 DNF → 75%.
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "A", "Club", "Male", ""],
            ["2", "B", "Club", "Female", ""],
            ["3", "C", "Club", "Male", ""],
            ["4", "D", "Club", "Female", ""],
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
            [["Position", "Bib"], ["1", "1"], ["2", "2"], ["3", "3"]]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:24:30\n3,x,00:29:00\n"));

        var summary = Service.GetRaceStatisticsSummary();

        Assert.Equal(4, summary.EntrantCount);
        Assert.Equal(3, summary.FinisherCount);
        Assert.Equal(1, summary.DnfCount);
        Assert.Equal(75.0, summary.CompletionRatePercent);
    }

    [Fact]
    public async Task TimeSummary_ComputesWinnerMedianAverageAndPercentiles()
    {
        // Times 20, 24, 30 minutes.
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "A", "Club", "Male", ""],
            ["2", "B", "Club", "Male", ""],
            ["3", "C", "Club", "Male", ""],
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
            [["Position", "Bib"], ["1", "1"], ["2", "2"], ["3", "3"]]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:24:00\n3,x,00:30:00\n"));

        var summary = Service.GetRaceStatisticsSummary();

        Assert.True(summary.HasTimes);
        Assert.Equal(new TimeSpan(0, 20, 0), summary.WinnerTime);
        Assert.Equal(new TimeSpan(0, 24, 0), summary.MedianTime);
        Assert.Equal(new TimeSpan(0, 24, 40), summary.AverageTime); // (20+24+30)/3 = 24:40
        Assert.Equal(new TimeSpan(0, 4, 0), summary.WinnerToMedianSpread);
        Assert.Equal(0, summary.ExcludedTimeRowCount);
    }

    [Fact]
    public async Task DnsEntrant_ExcludedFromStartersAndCompletion()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "A", "Club", "Male", ""],
            ["2", "B", "Club", "Male", ""], // will be DNS
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
            [["Position", "Bib"], ["1", "1"]]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", "STARTOFEVENT,x,x\n1,x,00:20:00\n"));

        Service.SetNonFinisherStatus("2", FinishStatus.DidNotStart);
        var summary = Service.GetRaceStatisticsSummary();

        Assert.Equal(1, summary.EntrantCount);   // DNS excluded from starters
        Assert.Equal(1, summary.DnsCount);
        Assert.Equal(0, summary.DnfCount);        // the DNS runner is not counted as DNF
        Assert.Equal(100.0, summary.CompletionRatePercent);
    }

    [Fact]
    public async Task BusiestWindow_IdentifiesPeakMinute()
    {
        // Three finishers in the 24th minute, one in the 20th → peak at minute 24.
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "A", "Club", "Male", ""],
            ["2", "B", "Club", "Male", ""],
            ["3", "C", "Club", "Male", ""],
            ["4", "D", "Club", "Male", ""],
        ])]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx",
            [["Position", "Bib"], ["1", "1"], ["2", "2"], ["3", "3"], ["4", "4"]]));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv",
            "STARTOFEVENT,x,x\n1,x,00:20:00\n2,x,00:24:10\n3,x,00:24:30\n4,x,00:24:50\n"));

        var summary = Service.GetRaceStatisticsSummary();

        Assert.Equal(24, summary.BusiestWindowMinute);
        Assert.Equal(3, summary.BusiestWindowCount);
    }
}
