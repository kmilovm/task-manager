using FluentValidation;
using NSubstitute;
using Shouldly;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Users;
using TaskManager.Domain.Users;

namespace TaskManager.Application.Tests.Users;

public class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly ITokenGenerator _tokens = Substitute.For<ITokenGenerator>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _clock.UtcNow.Returns(Now);
        _hasher.Hash(Arg.Any<string>()).Returns(callInfo => $"hashed:{callInfo.Arg<string>()}");
        _tokens.Generate(Arg.Any<User>()).Returns(new AccessToken("token", Now.AddMinutes(60)));

        _service = new AuthService(
            _users,
            _hasher,
            _tokens,
            _clock,
            new RegisterRequestValidator(),
            new LoginRequestValidator());
    }

    [Fact]
    public async Task RegisterAsync_WithNewEmail_CreatesUserAndReturnsToken()
    {
        var result = await _service.RegisterAsync(new RegisterRequest("Grace@Example.com", " Grace Hopper ", "Passw0rd!"));

        await _users.Received(1).AddAsync(
            Arg.Is<User>(user => user.Email.Value == "grace@example.com" && user.DisplayName == "Grace Hopper"),
            Arg.Any<CancellationToken>());

        result.AccessToken.ShouldBe("token");
        result.ExpiresAt.ShouldBe(Now.AddMinutes(60));
        result.User.Email.ShouldBe("grace@example.com");
        result.User.DisplayName.ShouldBe("Grace Hopper");
        result.User.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task RegisterAsync_StoresAHashInsteadOfThePassword()
    {
        await _service.RegisterAsync(new RegisterRequest("grace@example.com", "Grace Hopper", "Passw0rd!"));

        await _users.Received(1).AddAsync(
            Arg.Is<User>(user => user.PasswordHash == "hashed:Passw0rd!"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithExistingEmail_ThrowsEmailAlreadyInUse()
    {
        _users.ExistsWithEmailAsync(Email.Create("ada@example.com"), Arg.Any<CancellationToken>()).Returns(true);

        var exception = await Should.ThrowAsync<EmailAlreadyInUseException>(
            () => _service.RegisterAsync(new RegisterRequest("Ada@Example.com", "Ada Byron", "Passw0rd!")));

        exception.Message.ShouldBe("An account with this email already exists.");
        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WithAnInvalidRequest_ThrowsValidationExceptionWithoutTouchingTheRepository()
    {
        await Should.ThrowAsync<ValidationException>(
            () => _service.RegisterAsync(new RegisterRequest("not-an-email", "Grace Hopper", "weak")));

        await _users.DidNotReceive().AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsToken()
    {
        GivenRegisteredUser();

        var result = await _service.LoginAsync(new LoginRequest("ada@example.com", "Passw0rd!"));

        result.AccessToken.ShouldBe("token");
        result.User.Email.ShouldBe("ada@example.com");
    }

    [Fact]
    public async Task LoginAsync_IsCaseInsensitiveOnEmail()
    {
        GivenRegisteredUser();

        var result = await _service.LoginAsync(new LoginRequest("  ADA@Example.COM  ", "Passw0rd!"));

        result.User.Email.ShouldBe("ada@example.com");
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsInvalidCredentials()
    {
        GivenRegisteredUser();
        _hasher.Verify("wrong-password", Arg.Any<string>()).Returns(false);

        var exception = await Should.ThrowAsync<InvalidCredentialsException>(
            () => _service.LoginAsync(new LoginRequest("ada@example.com", "wrong-password")));

        exception.Message.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsInvalidCredentials()
    {
        var exception = await Should.ThrowAsync<InvalidCredentialsException>(
            () => _service.LoginAsync(new LoginRequest("nobody@example.com", "Passw0rd!")));

        exception.Message.ShouldBe("Invalid email or password.");
    }

    [Fact]
    public async Task LoginAsync_WithAMalformedEmail_ThrowsInvalidCredentialsRatherThanLeakingAValidationError()
    {
        await Should.ThrowAsync<InvalidCredentialsException>(
            () => _service.LoginAsync(new LoginRequest("not-an-email", "Passw0rd!")));
    }

    [Fact]
    public async Task GetProfileAsync_ReturnsTheAccountWithoutItsPasswordHash()
    {
        var user = GivenRegisteredUser();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var profile = await _service.GetProfileAsync(user.Id);

        profile.Id.ShouldBe(user.Id);
        profile.Email.ShouldBe("ada@example.com");
        profile.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task GetProfileAsync_WhenTheAccountIsGone_ThrowsNotFound()
    {
        await Should.ThrowAsync<NotFoundException>(() => _service.GetProfileAsync(Guid.NewGuid()));
    }

    private User GivenRegisteredUser()
    {
        var user = User.Register("ada@example.com", "Ada Lovelace", "hashed:Passw0rd!", Now);

        _users.GetByEmailAsync(Email.Create("ada@example.com"), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("Passw0rd!", "hashed:Passw0rd!").Returns(true);

        return user;
    }
}
