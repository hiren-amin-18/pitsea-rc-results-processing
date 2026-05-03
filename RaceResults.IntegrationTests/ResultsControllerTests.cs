using System.Net;
using RaceResults.Web.Data;
using RaceResults.Web.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace RaceResults.IntegrationTests;

/// <summary>
/// Integration tests for results, stats, top-10, edit, and PDF export pages.
/// Uses a separate factory instance so it can seed state independently.
/// </summary>
public class ResultsControllerTests : IClassFixture<RaceResultsWebFactory>
{
    private readonly HttpClient _client;
    private readonly RaceResultsWebFactory _factory;

    public ResultsControllerTests(RaceResultsWebFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task Results_Get_ReturnsOk()
    {
        var response = await _client.GetAsync("/Race/Results");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Stats_Get_ReturnsOk()
    {
        var response = await _client.GetAsync("/Race/Stats");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Top10_Get_ReturnsOk()
    {
        var response = await _client.GetAsync("/Race/Top10");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ExportPdf_Get_ReturnsPdfFile()
    {
        var response = await _client.GetAsync("/Race/ExportPdf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task EditResult_Get_UnknownPosition_RedirectsToResults()
    {
        var response = await _client.GetAsync("/Race/EditResult?position=9999");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Race/Results", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task EditResult_Get_KnownPosition_ReturnsOk()
    {
        await SeedDatabaseDirectly();
        var client = _factory.CreateClient(); // allow redirects for this one

        var response = await client.GetAsync("/Race/EditResult?position=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EditResult_Post_InvalidModel_ReturnsView()
    {
        await SeedDatabaseDirectly();

        // Use a single client so antiforgery cookies are shared
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenFromClientAsync(client, "/Race/EditResult?position=1");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("OriginalPosition", "1"),
            new KeyValuePair<string, string>("NewPosition", "0"),  // invalid — below minimum
            new KeyValuePair<string, string>("BibNumber", ""),       // required
        });

        var response = await client.PostAsync("/Race/EditResult", content);

        // Invalid model state — re-render the view (200)
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task EditResult_Post_Valid_RedirectsToResults()
    {
        await SeedDatabaseDirectly();

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenFromClientAsync(client, "/Race/EditResult?position=1");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("OriginalPosition", "1"),
            new KeyValuePair<string, string>("NewPosition", "1"),
            new KeyValuePair<string, string>("BibNumber", "1"),
            new KeyValuePair<string, string>("Time", "00:20:00"),
        });

        var response = await client.PostAsync("/Race/EditResult", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Race/Results", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// Seeds the test database directly (bypassing HTTP) to set up state for result tests.
    /// </summary>
    private async Task SeedDatabaseDirectly()
    {
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        await using var db = dbFactory.CreateDbContext();

        // Ensure schema exists (safe to call multiple times)
        db.Database.EnsureCreated();

        // Clear existing
        await db.TimingRows.ExecuteDeleteAsync();
        await db.FinishBibRecords.ExecuteDeleteAsync();
        await db.Entrants.ExecuteDeleteAsync();

        db.Entrants.AddRange(
            new Entrant { BibNumber = "1", Name = "Alice", Club = "Club A", Gender = "Female", Age = 20 },
            new Entrant { BibNumber = "2", Name = "Bob",   Club = "Club B", Gender = "Male",   Age = 22 }
        );
        db.FinishBibRecords.AddRange(
            new FinishBibRecord { Position = 1, BibNumber = "1" },
            new FinishBibRecord { Position = 2, BibNumber = "2" }
        );
        db.TimingRows.AddRange(
            new TimingRow { Position = 1, Time = "00:20:00" },
            new TimingRow { Position = 2, Time = "00:21:00" }
        );
        await db.SaveChangesAsync();
    }

    private async Task<string> GetAntiforgeryTokenAsync(string url)
    {
        return await GetAntiforgeryTokenFromClientAsync(_factory.CreateClient(), url);
    }

    private static async Task<string> GetAntiforgeryTokenFromClientAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url);
        var html = await response.Content.ReadAsStringAsync();

        var tokenStart = html.IndexOf("__RequestVerificationToken", StringComparison.Ordinal);
        if (tokenStart < 0) return string.Empty;

        var valueStart = html.IndexOf("value=\"", tokenStart, StringComparison.Ordinal) + 7;
        var valueEnd = html.IndexOf('"', valueStart);
        return html[valueStart..valueEnd];
    }
}
