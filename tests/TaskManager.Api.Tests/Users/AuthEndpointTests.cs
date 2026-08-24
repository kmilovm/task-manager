using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Users;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Security;

namespace TaskManager.Api.Tests.Users;

public sealed class AuthEndpointTests : IClassFixture<ApiFactory>, IAsyncLifetime
{
    private readonly ApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointTests(ApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Register_WithANewEmail_ReturnsCreatedWithAToken()
    {
        var response = await Register("grace@example.com", "Grace Hopper", "Passw0rd!");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var body = await response.Content.ReadFromJsonAsync<AuthResult>();
        body.ShouldNotBeNull();
        body.AccessToken.ShouldNotBeNullOrWhiteSpace();
        body.User.Email.ShouldBe("grace@example.com");
        body.User.DisplayName.ShouldBe("Grace Hopper");
    }

    [Fact]
    public async Task Register_NeverEchoesThePasswordOrItsHash()
    {
        var response = await Register("grace@example.com", "Grace Hopper", "Passw0rd!");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var payload = await response.Content.ReadAsStringAsync();

        payload.ShouldNotContain("Passw0rd!");
        payload.ShouldNotContain("passwordHash", Case.Insensitive);
    }

    [Fact]
    public async Task Register_WithAnEmailThatIsAlreadyTaken_ReturnsConflict()
    {
        await Register("ada@example.com", "Ada Lovelace", "Passw0rd!");

        var response = await Register("ADA@Example.com", "Ada Byron", "Passw0rd!");

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Problem(response)).Detail.ShouldBe("An account with this email already exists.");
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("password")]
    [InlineData("12345678")]
    public async Task Register_WithAWeakPassword_ReturnsAValidationProblem(string password)
    {
        var response = await Register("grace@example.com", "Grace Hopper", password);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ValidationErrors(response)).Keys.ShouldContain("password");
    }

    [Fact]
    public async Task Register_WithoutADisplayName_ReturnsAValidationProblem()
    {
        var response = await Register("grace@example.com", "  ", "Passw0rd!");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ValidationErrors(response)).Keys.ShouldContain("displayName");
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAToken()
    {
        await Register("ada@example.com", "Ada Lovelace", "Passw0rd!");

        var response = await Login("ada@example.com", "Passw0rd!");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<AuthResult>())!.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_MatchesTheEmailRegardlessOfCasing()
    {
        await Register("ada@example.com", "Ada Lovelace", "Passw0rd!");

        var response = await Login("  ADA@Example.COM  ", "Passw0rd!");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Login_WithAWrongPassword_ReturnsUnauthorized()
    {
        await Register("ada@example.com", "Ada Lovelace", "Passw0rd!");

        var response = await Login("ada@example.com", "wrong-password");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await Problem(response)).Detail.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task Login_WithAnUnknownEmail_FailsExactlyLikeAWrongPassword()
    {
        await Register("ada@example.com", "Ada Lovelace", "Passw0rd!");

        var unknown = await Login("nobody@example.com", "Passw0rd!");
        var wrongPassword = await Login("ada@example.com", "wrong-password");

        unknown.StatusCode.ShouldBe(wrongPassword.StatusCode);
        (await Problem(unknown)).Detail.ShouldBe((await Problem(wrongPassword)).Detail);
    }

    [Fact]
    public async Task GetMe_WhenSignedIn_ReturnsProfile()
    {
        var token = await RegisterAndSignIn();

        var response = await Authorised(token).GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var profile = await response.Content.ReadFromJsonAsync<UserDto>();
        profile.ShouldNotBeNull();
        profile.Email.ShouldBe("ada@example.com");
        profile.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task GetMe_WithoutAToken_ReturnsUnauthorized()
    {
        (await _client.GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithAnExpiredToken_ReturnsUnauthorized()
    {
        var response = await Authorised(ExpiredTokenFor(User.Register(
            "ada@example.com", "Ada Lovelace", "hash", DateTimeOffset.UtcNow))).GetAsync("/api/auth/me");

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithATokenSignedByADifferentKey_ReturnsUnauthorized()
    {
        var foreignKey = new JwtOptions
        {
            Issuer = _factory.Jwt.Issuer,
            Audience = _factory.Jwt.Audience,
            SigningKey = "a-completely-different-signing-key-value",
            AccessTokenLifetimeMinutes = 60,
        };

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);

        var token = new JwtTokenGenerator(Options.Create(foreignKey), clock)
            .Generate(User.Register("ada@example.com", "Ada Lovelace", "hash", DateTimeOffset.UtcNow));

        (await Authorised(token.Value).GetAsync("/api/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetPublic_WithoutAToken_ReturnsOk()
    {
        (await _client.GetAsync("/api/auth/public")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private Task<HttpResponseMessage> Register(string email, string displayName, string password) =>
        _client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, displayName, password));

    private Task<HttpResponseMessage> Login(string email, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));

    private async Task<string> RegisterAndSignIn()
    {
        var response = await Register("ada@example.com", "Ada Lovelace", "Passw0rd!");

        return (await response.Content.ReadFromJsonAsync<AuthResult>())!.AccessToken;
    }

    private HttpClient Authorised(string token)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private string ExpiredTokenFor(User user)
    {
        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow.AddHours(-2));

        return new JwtTokenGenerator(Options.Create(_factory.Jwt), clock).Generate(user).Value;
    }

    private static async Task<ProblemResponse> Problem(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ProblemResponse>())!;

    private static async Task<Dictionary<string, string[]>> ValidationErrors(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<ValidationProblemResponse>())!.Errors;

    private sealed record ProblemResponse(string Title, string Detail, int Status);

    private sealed record ValidationProblemResponse(string Title, int Status, Dictionary<string, string[]> Errors);
}
