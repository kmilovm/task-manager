using System.Net;
using Shouldly;

namespace TaskManager.Api.Tests.Health;

public sealed class HealthEndpointTests : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ApiFactory factory) => _client = factory.CreateClient();

    [Fact]
    public async Task GetHealth_WithoutToken_ReturnsOk()
    {
        (await _client.GetAsync("/api/health")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
