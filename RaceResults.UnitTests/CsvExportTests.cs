using System.Text;
using System.Text.RegularExpressions;
using RaceResults.UnitTests.Helpers;

namespace RaceResults.UnitTests;

public class CsvExportTests : RaceResultsServiceTestBase
{
    private static readonly string[] EntrantHeader = ["Bib", "Name", "Club", "Gender", "Age"];

    [Fact]
    public async Task GenerateResultsCsv_HasHeaderAndFinishersOnly()
    {
        await SeedRaceWithDnf();

        var text = Decode(Service.GenerateResultsCsv());
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.TrimEnd('\r'))
                        .ToArray();

        Assert.Equal("Position,Time,Bib,Name,Club,Gender,Age,Status", lines[0]);
        // Two finishers, and the DNF entrant (Carol) is omitted.
        Assert.Contains(lines, l => l.StartsWith("1,") && l.EndsWith(",Finished"));
        Assert.Contains(lines, l => l.StartsWith("2,") && l.EndsWith(",Finished"));
        Assert.DoesNotContain(lines, l => l.EndsWith(",DNF"));
        Assert.DoesNotContain("Carol", text);
    }

    [Fact]
    public async Task GenerateResultsCsv_EscapesCommasInClubNames()
    {
        await SeedRaceWithDnf();

        var text = Decode(Service.GenerateResultsCsv());

        // CsvHelper quotes fields that contain a comma (AC7).
        Assert.Contains("\"Run, Club\"", text);
    }

    [Fact]
    public async Task GenerateResultsCsv_StartsWithUtf8Bom()
    {
        await SeedRaceWithDnf();

        var bytes = Service.GenerateResultsCsv();

        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public void GetResultsCsvFileName_IsDescriptive()
    {
        var name = Service.GetResultsCsvFileName();

        Assert.Matches(@"^crown-to-crown-\d{4}-\d{2}-\d{2}-results\.csv$", name);
    }

    private static string Decode(byte[] bytes)
    {
        using var reader = new StreamReader(new MemoryStream(bytes), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private async Task SeedRaceWithDnf()
    {
        await Service.UploadEntrantsAsync([FormFileHelpers.CreateXlsx("e.xlsx",
        [
            EntrantHeader,
            ["1", "Alice", "Run, Club", "Female", "20"],
            ["2", "Bob",   "Club B",    "Male",   "22"],
            ["3", "Carol", "Club C",    "Female", "30"], // no finish row → DNF
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
