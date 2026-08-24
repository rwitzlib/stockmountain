using FluentAssertions;
using MarketViewer.Contracts.Enums;
using Xunit;

namespace MarketViewer.Contracts.UnitTests.Enums;

public class UserRoleParserUnitTests
{
    [Theory]
    [InlineData("Free", UserRole.Free)]
    [InlineData("Pro", UserRole.Pro)]
    [InlineData("Premium", UserRole.Premium)]
    public void Parse_CurrentNames_ReturnsRole(string stored, UserRole expected)
    {
        UserRoleParser.Parse(stored).Should().Be(expected);
    }

    [Theory]
    [InlineData("Basic", UserRole.Free)]
    [InlineData("Advanced", UserRole.Pro)]
    public void Parse_LegacyStoredNames_MapsToRenamedRole(string stored, UserRole expected)
    {
        UserRoleParser.Parse(stored).Should().Be(expected);
    }

    [Fact]
    public void Parse_UnknownName_Throws()
    {
        var act = () => UserRoleParser.Parse("Enterprise");

        act.Should().Throw<ArgumentException>();
    }
}
