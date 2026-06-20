using QuestPDF.Infrastructure;
using RaceResults.UnitTests.Helpers;
using RaceResults.Web.Models;
using RaceResults.Web.Services;

namespace RaceResults.UnitTests;

/// <summary>
/// US33 — Bluebell 5 results processing. Parser dispatches on event type; vet status derives
/// from the Age column ('Male U40' / 'Female U35' / blank); Top10 and PDF winners differ for
/// Bluebell events.
/// </summary>
public class BluebellResultsTests : RaceResultsServiceTestBase
{
    static BluebellResultsTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly string[] BluebellHeader = ["Race No", "Name", "Club Name", "M/F", "Age"];

    public BluebellResultsTests()
    {
        Service.CreateEvent(new CreateEventInput
        {
            EventName = "Bluebell 5 2026",
            EventDate = new DateTime(2026, 5, 17),
            EventType = EventType.Bluebell5
        });
    }

    [Fact]
    public async Task Upload_DerivesVetFromAgeColumn()
    {
        var file = FormFileHelpers.CreateXlsx("entries.xlsx",
        [
            BluebellHeader,
            ["1", "Senior Male",   "ClubA", "Male",   "Male U40"],
            ["2", "Senior Female", "ClubB", "Female", "Female U35"],
            ["3", "Vet Male",      "ClubC", "Male",   ""],
            ["4", "Vet Female",    "ClubD", "Female", ""],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.True(result.Success, string.Join("; ", result.Errors));

        await SeedFinishTimings(("1", "00:30:00"), ("2", "00:35:00"), ("3", "00:31:00"), ("4", "00:36:00"));
        var categories = Service.GetTopTenByCategory();
        var vetMale = categories.Single(c => c.Name == "Vet Male");
        var vetFemale = categories.Single(c => c.Name == "Vet Female");
        Assert.Single(vetMale.Results);
        Assert.Equal("Vet Male", vetMale.Results[0].Name);
        Assert.Single(vetFemale.Results);
        Assert.Equal("Vet Female", vetFemale.Results[0].Name);
    }

    [Fact]
    public async Task Upload_RejectsU18Row()
    {
        var file = FormFileHelpers.CreateXlsx("entries.xlsx",
        [
            BluebellHeader,
            ["1", "Adult",     "Club", "Male", "Male U40"],
            ["2", "Youngster", "Club", "Male", "Male U18"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("Male U18", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Upload_RejectsAgeCategoryMismatchedToGender()
    {
        var file = FormFileHelpers.CreateXlsx("entries.xlsx",
        [
            BluebellHeader,
            ["1", "Mismatch", "Club", "Female", "Male U40"],
        ]);

        var result = await Service.UploadEntrantsAsync([file]);

        Assert.False(result.Success);
        Assert.Contains(result.Errors, e => e.Contains("does not match"));
    }

    [Fact]
    public async Task TopTen_Bluebell_ReplacesU18WithVet()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("entries.xlsx",
        [
            BluebellHeader,
            ["1", "Alice",  "Club", "Female", "Female U35"],
            ["2", "Bob",    "Club", "Male",   "Male U40"],
            ["3", "Vetman", "Club", "Male",   ""],
        ])]);
        await SeedFinishTimings(("1", "00:30:00"), ("2", "00:31:00"), ("3", "00:32:00"));

        var categories = Service.GetTopTenByCategory();

        Assert.Equal(4, categories.Count);
        Assert.Contains(categories, c => c.Name == "Male");
        Assert.Contains(categories, c => c.Name == "Female");
        Assert.Contains(categories, c => c.Name == "Vet Male");
        Assert.Contains(categories, c => c.Name == "Vet Female");
        Assert.DoesNotContain(categories, c => c.Name.Contains("U18"));
    }

    [Fact]
    public async Task WinnerSelection_VetPrizeSkipsTop3()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("entries.xlsx",
        [
            BluebellHeader,
            // A vet inside the male top 3 (M2 Vet) must NOT also win the vet prize.
            ["1", "M1 Senior", "Club", "Male",   "Male U40"],
            ["2", "M2 Vet",    "Club", "Male",   ""],
            ["3", "M3 Senior", "Club", "Male",   "Male U40"],
            ["4", "M4 Vet",    "Club", "Male",   ""], // first vet outside the top 3
            // Same shape for female.
            ["5", "F1 Senior", "Club", "Female", "Female U35"],
            ["6", "F2 Vet",    "Club", "Female", ""],
            ["7", "F3 Senior", "Club", "Female", "Female U35"],
            ["8", "F4 Vet",    "Club", "Female", ""],
        ])]);
        await SeedFinishTimings(
            ("1", "00:30:00"), ("2", "00:31:00"), ("3", "00:32:00"), ("4", "00:33:00"),
            ("5", "00:40:00"), ("6", "00:41:00"), ("7", "00:42:00"), ("8", "00:43:00"));

        var collated = Service.GetCollatedResults();
        var winners = BluebellWinnerSelection.Select(collated);

        Assert.Equal(new[] { "M1 Senior", "M2 Vet", "M3 Senior" }, winners.MaleTop3.Select(r => r.Name).ToArray());
        Assert.Equal(new[] { "F1 Senior", "F2 Vet", "F3 Senior" }, winners.FemaleTop3.Select(r => r.Name).ToArray());
        Assert.Equal("M4 Vet", winners.VetMale?.Name);
        Assert.Equal("F4 Vet", winners.VetFemale?.Name);
    }

    [Fact]
    public async Task WinnerSelection_EmptyVetSlotIsNull()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("entries.xlsx",
        [
            BluebellHeader,
            ["1", "M1", "Club", "Male",   "Male U40"],
            ["2", "F1", "Club", "Female", "Female U35"],
        ])]);
        await SeedFinishTimings(("1", "00:30:00"), ("2", "00:31:00"));

        var winners = BluebellWinnerSelection.Select(Service.GetCollatedResults());

        Assert.Null(winners.VetMale);
        Assert.Null(winners.VetFemale);
    }

    [Fact]
    public async Task GenerateResultsPdf_Bluebell_ProducesPdf()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("entries.xlsx",
        [
            BluebellHeader,
            ["1", "M1", "Club", "Male",   "Male U40"],
            ["2", "F1", "Club", "Female", ""],
        ])]);
        await SeedFinishTimings(("1", "00:30:00"), ("2", "00:35:00"));

        var pdf = Service.GenerateResultsPdf();

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 0);
        Assert.Equal(0x25, pdf[0]); // '%'
        Assert.Equal(0x50, pdf[1]); // 'P'
    }

    private async Task SeedFinishTimings(params (string Bib, string Time)[] rows)
    {
        var finishRows = new List<string[]> { new[] { "Position", "Bib" } };
        for (var i = 0; i < rows.Length; i++)
        {
            finishRows.Add(new[] { $"{i + 1}", rows[i].Bib });
        }
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("fb.xlsx", finishRows));

        var csvLines = "STARTOFEVENT,x,x\n" +
            string.Join("\n", Enumerable.Range(1, rows.Length).Select(i => $"{i},x,{rows[i - 1].Time}"));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("t.csv", csvLines));
    }
}
