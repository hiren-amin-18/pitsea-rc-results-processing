using System.Net;

namespace RaceResults.IntegrationTests;

/// <summary>
/// Integration tests for the Home controller dashboard.
/// </summary>
public class HomeControllerTests : IClassFixture<RaceResultsWebFactory>
{
    private readonly HttpClient _client;

    public HomeControllerTests(RaceResultsWebFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Index_ReturnsOk()
    {
        var response = await _client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Index_ContainsDashboardHeading()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("Race Results", html);
    }
}
