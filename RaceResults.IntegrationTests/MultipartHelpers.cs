using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ClosedXML.Excel;

namespace RaceResults.IntegrationTests;

/// <summary>
/// Helpers for building multipart/form-data content for file uploads in integration tests.
/// </summary>
internal static class MultipartHelpers
{
    public static MultipartFormDataContent BuildXlsxUpload(string fieldName, string fileName, IEnumerable<string[]> rows)
    {
        var ms = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("Sheet1");
            int rowNum = 1;
            foreach (var row in rows)
            {
                for (int col = 0; col < row.Length; col++)
                    sheet.Cell(rowNum, col + 1).SetValue(row[col]);
                rowNum++;
            }
            workbook.SaveAs(ms);
        }
        ms.Position = 0;

        var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(ms);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        content.Add(fileContent, fieldName, fileName);
        return content;
    }

    public static MultipartFormDataContent BuildCsvUpload(string fieldName, string fileName, string csvContent)
    {
        var bytes = Encoding.UTF8.GetBytes(csvContent);
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(fileContent, fieldName, fileName);
        return content;
    }
}
