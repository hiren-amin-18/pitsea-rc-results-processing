using System.Net;

namespace RaceResults.IntegrationTests;

/// <summary>
/// Integration tests for upload endpoints (GET pages and POST actions).
/// </summary>
public class UploadControllerTests : IClassFixture<RaceResultsWebFactory>
{
    private readonly HttpClient _client;
    private readonly RaceResultsWebFactory _factory;

    public UploadControllerTests(RaceResultsWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Uploads_Get_ReturnsOk()
    {
        var response = await _client.GetAsync("/Race/Uploads");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Uploads_Get_ContainsUploadForms()
    {
        var response = await _client.GetAsync("/Race/Uploads");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Entrant", html);
        Assert.Contains("Finish", html);
        Assert.Contains("Timing", html);
    }

    [Fact]
    public async Task UploadEntrants_Post_ValidXlsx_RedirectsToUploads()
    {
        // Use a single client so antiforgery cookies are shared between GET and POST
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Race/Uploads");
        var content = MultipartHelpers.BuildXlsxUpload("files", "entrants.xlsx",
        [
            ["Bib", "Name", "Club", "Gender", "Age"],
            ["1", "Alice", "Club A", "Female", "30"],
        ]);
        content.Add(new StringContent(token), "__RequestVerificationToken");

        var response = await client.PostAsync("/Race/UploadEntrants", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Race/Uploads", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task UploadFinishBib_Post_WithoutEntrants_RedirectsWithError()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Race/Uploads");
        var content = MultipartHelpers.BuildXlsxUpload("file", "finish.xlsx",
        [
            ["Position", "Bib"],
            ["1", "1"],
        ]);
        content.Add(new StringContent(token), "__RequestVerificationToken");

        var response = await client.PostAsync("/Race/UploadFinishBib", content);

        // Should redirect back to Uploads (even on failure, StoreFeedback sets TempData)
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    [Fact]
    public async Task UploadTimings_Post_WithoutFinishBib_Redirects()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Race/Uploads");
        var csv = "STARTOFEVENT,x,x\n1,x,00:20:00\n";
        var content = MultipartHelpers.BuildCsvUpload("file", "timings.csv", csv);
        content.Add(new StringContent(token), "__RequestVerificationToken");

        var response = await client.PostAsync("/Race/UploadTimings", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        // Extract the antiforgery token from the hidden input
        var tokenStart = html.IndexOf("__RequestVerificationToken", StringComparison.Ordinal);
        if (tokenStart < 0) return string.Empty;

        var valueStart = html.IndexOf("value=\"", tokenStart, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }
}
