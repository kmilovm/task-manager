using Shouldly;
using TaskManager.Domain.Common;
using TaskManager.Domain.Users;

namespace TaskManager.Domain.Tests.Users;

public class EmailTests
{
    [Theory]
    [InlineData("ada@example.com", "ada@example.com")]
    [InlineData("ADA@Example.COM", "ada@example.com")]
    [InlineData("  ada@example.com  ", "ada@example.com")]
    public void Create_NormalisesTheAddress(string input, string expected)
    {
        Email.Create(input).Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    [InlineData("two@@example.com")]
    [InlineData("spaces in@example.com")]
    public void Create_WithAnInvalidAddress_Throws(string? input)
    {
        Should.Throw<DomainException>(() => Email.Create(input!));
    }

    [Fact]
    public void Create_WithAnAddressLongerThanTheColumn_Throws()
    {
        var local = new string('a', 250);

        Should.Throw<DomainException>(() => Email.Create($"{local}@example.com"));
    }

    [Fact]
    public void Equality_IgnoresCasing()
    {
        Email.Create("ADA@example.com").ShouldBe(Email.Create("ada@example.com"));
    }

    [Fact]
    public void ToString_ReturnsTheNormalisedAddress()
    {
        Email.Create(" Ada@Example.com ").ToString().ShouldBe("ada@example.com");
    }
}
