using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NSubstitute;
using Shouldly;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Security;

namespace TaskManager.Infrastructure.Tests.Security;

public class BCryptPasswordHasherTests
{
    private readonly BCryptPasswordHasher _hasher = new();

    [Fact]
    public void Hash_DoesNotReturnThePasswordInClear()
    {
        _hasher.Hash("Passw0rd!").ShouldNotBe("Passw0rd!");
    }

    [Fact]
    public void Hash_SaltsEachCallSoTheSamePasswordHashesDifferently()
    {
        _hasher.Hash("Passw0rd!").ShouldNotBe(_hasher.Hash("Passw0rd!"));
    }

    [Fact]
    public void Verify_WithTheCorrectPassword_ReturnsTrue()
    {
        _hasher.Verify("Passw0rd!", _hasher.Hash("Passw0rd!")).ShouldBeTrue();
    }

    [Fact]
    public void Verify_WithAWrongPassword_ReturnsFalse()
    {
        _hasher.Verify("wrong-password", _hasher.Hash("Passw0rd!")).ShouldBeFalse();
    }

    [Fact]
    public void Verify_WithAHashItDidNotProduce_ReturnsFalseInsteadOfThrowing()
    {
        _hasher.Verify("Passw0rd!", "not-a-bcrypt-hash").ShouldBeFalse();
    }
}

public class JwtTokenGeneratorTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly JwtOptions _options = new()
    {
        Issuer = "taskmanager",
        Audience = "taskmanager-client",
        SigningKey = "a-signing-key-long-enough-for-hmac-sha256",
        AccessTokenLifetimeMinutes = 60,
    };

    private readonly IClock _clock = Substitute.For<IClock>();

    public JwtTokenGeneratorTests() => _clock.UtcNow.Returns(Now);

    [Fact]
    public void Generate_ExpiresAfterTheConfiguredLifetime()
    {
        var token = Generator().Generate(AUser());

        token.ExpiresAt.ShouldBe(Now.AddMinutes(60));
    }

    [Fact]
    public void Generate_CarriesTheAccountIdentityAsClaims()
    {
        var user = AUser();

        var token = new JsonWebTokenHandler().ReadJsonWebToken(Generator().Generate(user).Value);

        token.GetClaim(JwtRegisteredClaimNames.Sub).Value.ShouldBe(user.Id.ToString());
        token.GetClaim(JwtRegisteredClaimNames.Email).Value.ShouldBe("ada@example.com");
        token.Issuer.ShouldBe("taskmanager");
        token.Audiences.ShouldContain("taskmanager-client");
    }

    [Fact]
    public void Generate_NeverPutsThePasswordHashInTheToken()
    {
        var token = Generator().Generate(AUser());

        token.Value.ShouldNotContain("secret-hash");
    }

    [Fact]
    public async Task Generate_ProducesATokenThatValidatesAgainstTheSigningKey()
    {
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(
            Generator().Generate(AUser()).Value,
            new TokenValidationParameters
            {
                ValidIssuer = _options.Issuer,
                ValidAudience = _options.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                LifetimeValidator = (_, _, _, _) => true,
            });

        result.IsValid.ShouldBeTrue();
    }

    private JwtTokenGenerator Generator() => new(Options.Create(_options), _clock);

    private static User AUser() => User.Register("ada@example.com", "Ada Lovelace", "secret-hash", Now);
}
