using Shouldly;
using TaskManager.Domain.Common;
using TaskManager.Domain.Users;

namespace TaskManager.Domain.Tests.Users;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Register_NormalisesTheEmail()
    {
        var user = Register(email: "  ADA@Example.com ");

        user.Email.Value.ShouldBe("ada@example.com");
    }

    [Fact]
    public void Register_TrimsDisplayName()
    {
        var user = Register(displayName: "  Ada Lovelace  ");

        user.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WithoutADisplayName_Throws(string? displayName)
    {
        Should.Throw<DomainException>(() => Register(displayName: displayName!));
    }

    [Fact]
    public void Register_WithADisplayNameLongerThan100Characters_Throws()
    {
        Should.Throw<DomainException>(() => Register(displayName: new string('a', 101)));
    }

    [Fact]
    public void Register_WithADisplayNameOfExactly100Characters_Succeeds()
    {
        var user = Register(displayName: new string('a', 100));

        user.DisplayName.Length.ShouldBe(100);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_WithoutAPasswordHash_Throws(string? passwordHash)
    {
        Should.Throw<DomainException>(() => Register(passwordHash: passwordHash!));
    }

    [Fact]
    public void Register_AssignsAnIdentifierAndTheGivenCreationTime()
    {
        var user = Register();

        user.Id.ShouldNotBe(Guid.Empty);
        user.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Register_GivesEveryUserADistinctIdentifier()
    {
        Register().Id.ShouldNotBe(Register().Id);
    }

    private static User Register(
        string email = "ada@example.com",
        string displayName = "Ada Lovelace",
        string passwordHash = "hashed-password") =>
        User.Register(email, displayName, passwordHash, Now);
}
