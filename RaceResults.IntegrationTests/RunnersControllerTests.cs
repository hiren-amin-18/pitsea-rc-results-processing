using System.Net;

namespace RaceResults.IntegrationTests;

public class RunnersControllerTests : IClassFixture<RaceResultsWebFactory>
{
    private readonly RaceResultsWebFactory _factory;

    public RunnersControllerTests(RaceResultsWebFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Runners_Index_ReturnsOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/Runners");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Runners", html);
    }
}
