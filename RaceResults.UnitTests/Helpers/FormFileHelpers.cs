using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace RaceResults.UnitTests.Helpers;

/// <summary>
/// Factory methods for creating IFormFile test doubles.
/// </summary>
internal static class FormFileHelpers
{
    /// <summary>
    /// Builds an IFormFile backed by an in-memory XLSX workbook.
    /// <paramref name="rows"/> is a list of string arrays; the first is the header row.
    /// </summary>
    public static IFormFile CreateXlsx(string fileName, IEnumerable<string[]> rows)
    {
        var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Sheet1");
            int rowNum = 1;
            foreach (var row in rows)
            {
                for (int col = 0; col < row.Length; col++)
                {
                    sheet.Cell(rowNum, col + 1).SetValue(row[col]);
                }
                rowNum++;
            }
            workbook.SaveAs(ms);
        }
        ms.Position = 0;
        return new FormFile(ms, 0, ms.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        };
    }

    /// <summary>
    /// Builds an IFormFile backed by an in-memory CSV byte stream.
    /// </summary>
    public static IFormFile CreateCsv(string fileName, string csvContent)
    {
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        var ms = new MemoryStream(bytes);
        return new FormFile(ms, 0, ms.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "text/csv"
        };
    }
}
