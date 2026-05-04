using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RaceResults.Web.Data;
using RaceResults.Web.Models;

namespace RaceResults.IntegrationTests;

public class EventsControllerTests : IClassFixture<RaceResultsWebFactory>
{
    private readonly RaceResultsWebFactory _factory;

    public EventsControllerTests(RaceResultsWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Events_Index_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Events");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Create_Post_Valid_RedirectsAndAddsEvent()
    {
        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Events/Create");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
            new KeyValuePair<string, string>("EventName", "Bluebell 5 2026"),
            new KeyValuePair<string, string>("EventDate", "2026-05-01"),
            new KeyValuePair<string, string>("EventType", "1"),
        });

        var response = await client.PostAsync("/Events/Create", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Events", response.Headers.Location?.ToString());

        var html = await (await client.GetAsync("/Events")).Content.ReadAsStringAsync();
        Assert.Contains("Bluebell 5 2026", html);
    }

    [Fact]
    public async Task SetCurrent_Post_SwitchesCurrentEventInLayout()
    {
        var current = await GetCurrentEventAsync();
        var targetId = await AddEventAsync(new RaceEvent
        {
            EventName = "Target Event",
            EventDate = new DateTime(2026, 6, 1),
            EventType = EventType.CrownToCrown,
            IsCurrent = false
        });

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Events");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        });

        var response = await client.PostAsync($"/Events/SetCurrent/{targetId}", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var homeHtml = await (await client.GetAsync("/")).Content.ReadAsStringAsync();
        Assert.Contains("Current: Target Event", homeHtml);

        var originalCurrent = await FindEventByIdAsync(current.Id);
        var newCurrent = await FindEventByIdAsync(targetId);
        Assert.NotNull(originalCurrent);
        Assert.NotNull(newCurrent);
        Assert.False(originalCurrent!.IsCurrent);
        Assert.True(newCurrent!.IsCurrent);
    }

    [Fact]
    public async Task Delete_Post_RemovesEventAndAssociatedData()
    {
        var eventId = await AddEventAsync(new RaceEvent
        {
            EventName = "Delete Me",
            EventDate = new DateTime(2026, 7, 1),
            EventType = EventType.Bluebell5,
            IsCurrent = false
        });

        await SeedEntrantForEventAsync(eventId, "99");

        var client = _factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        var token = await GetAntiforgeryTokenAsync(client, "/Events");

        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("__RequestVerificationToken", token),
        });

        var response = await client.PostAsync($"/Events/Delete/{eventId}", content);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        await using var db = dbFactory.CreateDbContext();

        Assert.False(await db.Events.AnyAsync(e => e.Id == eventId));
        Assert.False(await db.Entrants.AnyAsync(e => e.EventId == eventId));
    }

    private async Task<int> AddEventAsync(RaceEvent raceEvent)
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        await using var db = dbFactory.CreateDbContext();

        db.Events.Add(raceEvent);
        await db.SaveChangesAsync();
        return raceEvent.Id;
    }

    private async Task SeedEntrantForEventAsync(int eventId, string bib)
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        await using var db = dbFactory.CreateDbContext();

        db.Entrants.Add(new Entrant
        {
            EventId = eventId,
            BibNumber = bib,
            Name = "Runner",
            Club = "Club Z",
            Gender = "Male",
            Age = 20
        });
        await db.SaveChangesAsync();
    }

    private async Task<RaceEvent> GetCurrentEventAsync()
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        await using var db = dbFactory.CreateDbContext();

        return await db.Events.SingleAsync(e => e.IsCurrent);
    }

    private async Task<RaceEvent?> FindEventByIdAsync(int id)
    {
        _ = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<RaceResultsDbContext>>();
        await using var db = dbFactory.CreateDbContext();

        return await db.Events.FirstOrDefaultAsync(e => e.Id == id);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
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
