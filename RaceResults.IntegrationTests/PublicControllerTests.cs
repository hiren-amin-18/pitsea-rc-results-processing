using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.IntegrationTests;

public class PublicControllerTests : IClassFixture<RaceResultsWebFactory>
{
    private readonly RaceResultsWebFactory _factory;

    public PublicControllerTests(RaceResultsWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnknownToken_Returns404()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/public/results/notarealtoken");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnpublishedEvent_Returns404()
    {
        var client = _factory.CreateClient();
        var token = await SeedPublishedEventAsync(publish: false);
        var response = await client.GetAsync($"/public/results/{token}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PublishedEvent_ReturnsOkWithResults()
    {
        var client = _factory.CreateClient();
        var token = await SeedPublishedEventAsync(publish: true);

        var response = await client.GetAsync($"/public/results/{token}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Alice", html);
    }

    [Fact]
    public async Task UnmatchedBibs_RenderedAsUnknownRunner()
    {
        var client = _factory.CreateClient();
        var token = await SeedPublishedEventAsync(publish: true, withUnmatched: true);

        var response = await client.GetAsync($"/public/results/{token}");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Unknown runner", html);
        // The internal "Unmatched" warning badge must not appear on the public page.
        Assert.DoesNotContain("Unmatched", html);
    }

    private async Task<string> SeedPublishedEventAsync(bool publish, bool withUnmatched = false)
    {
        using var scope = _factory.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        await db.TimingRows.ExecuteDeleteAsync();
        await db.FinishBibRecords.ExecuteDeleteAsync();
        await db.Entrants.ExecuteDeleteAsync();

        db.Entrants.AddRange(
            new Entrant { BibNumber = "1", Name = "Alice", Club = "Club A", Gender = "Female", Age = 20 }
        );
        if (!withUnmatched)
        {
            db.Entrants.Add(new Entrant { BibNumber = "2", Name = "Bob", Club = "Club B", Gender = "Male", Age = 22 });
        }
        db.FinishBibRecords.AddRange(
            new FinishBibRecord { Position = 1, BibNumber = "1" },
            new FinishBibRecord { Position = 2, BibNumber = "2" } // unmatched when Bob is omitted
        );
        db.TimingRows.AddRange(
            new TimingRow { Position = 1, Time = "00:20:00", DurationTicks = new TimeSpan(0, 20, 0).Ticks },
            new TimingRow { Position = 2, Time = "00:21:00", DurationTicks = new TimeSpan(0, 21, 0).Ticks }
        );

        var raceEvent = db.Events.First();
        var token = Guid.NewGuid().ToString("N");
        raceEvent.IsPublished = publish;
        raceEvent.PublicToken = token;
        await db.SaveChangesAsync();
        return token;
    }
}
