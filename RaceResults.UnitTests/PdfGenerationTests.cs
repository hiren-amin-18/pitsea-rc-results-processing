using QuestPDF.Infrastructure;
using RaceResults.UnitTests.Helpers;
using System.Text;

namespace RaceResults.UnitTests;

public class PdfGenerationTests : RaceResultsServiceTestBase
{
    static PdfGenerationTests()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public async Task GenerateResultsPdf_WithData_ReturnsPdfBytes()
    {
        await SeedFullRace();

        var bytes = Service.GenerateResultsPdf();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        // PDF magic bytes
        Assert.Equal(0x25, bytes[0]); // '%'
        Assert.Equal(0x50, bytes[1]); // 'P'
        Assert.Equal(0x44, bytes[2]); // 'D'
        Assert.Equal(0x46, bytes[3]); // 'F'
    }

    [Fact]
    public void GenerateResultsPdf_NoData_StillReturnsPdf()
    {
        var bytes = Service.GenerateResultsPdf();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task GenerateResultsPdf_LargeResultSet_ReturnsNonTrivialPdf()
    {
        await SeedLargeRace(95);

        var bytes = Service.GenerateResultsPdf();

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 8000);
        Assert.Equal(0x25, bytes[0]); // '%'
        Assert.Equal(0x50, bytes[1]); // 'P'
        Assert.Equal(0x44, bytes[2]); // 'D'
        Assert.Equal(0x46, bytes[3]); // 'F'
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

    private async Task SeedLargeRace(int count)
    {
        var entrantRows = new List<string[]> { EntrantHeader };
        var finishRows = new List<string[]> { new[] { "Position", "Bib" } };
        var timings = new StringBuilder("STARTOFEVENT,x,x\n");

        for (var i = 1; i <= count; i++)
        {
            entrantRows.Add(new[]
            {
                $"{i}",
                $"Runner {i}",
                i % 7 == 0 ? string.Empty : $"Club {i % 9}",
                i % 3 == 0 ? "Female" : "Male",
                (18 + (i % 35)).ToString()
            });
            finishRows.Add(new[] { $"{i}", $"{i}" });
            timings.AppendLine($"{i},x,00:{(15 + (i / 60)).ToString("00")}:{(i % 60).ToString("00")}");
        }

        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("large-entrants.xlsx", entrantRows)]);
        await Service.UploadFinishBibAsync(FormFileHelpers.CreateXlsx("large-finish.xlsx", finishRows));
        await Service.UploadTimingsAsync(FormFileHelpers.CreateCsv("large-timings.csv", timings.ToString()));
    }
}
