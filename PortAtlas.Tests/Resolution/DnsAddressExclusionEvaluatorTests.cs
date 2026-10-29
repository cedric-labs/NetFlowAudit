using System.Net;
using AwesomeAssertions;
using PortAtlas.Core.Resolution;
using Xunit;

namespace PortAtlas.Tests.Resolution;

public sealed class DnsAddressExclusionEvaluatorTests
{
    [Theory]
    [InlineData("169.254.1.1", true)]
    [InlineData("169.254.254.254", true)]
    [InlineData("224.0.0.1", true)]
    [InlineData("239.255.255.250", true)]
    [InlineData("255.255.255.255", true)]
    [InlineData("0.0.0.0", true)]
    [InlineData("192.168.1.50", false)]
    [InlineData("8.8.8.8", false)]
    [InlineData("140.82.121.4", false)]
    public void ShouldExclude_WhenEvaluatingConfiguredRules_ReturnsExpectedResult(string ipString, bool expectedResult)
    {
        // Arrange
        var rules = new[]
        {
            "0.0.0.0",
            "255.255.255.255",
            "169.254.0.0/16",
            "224.0.0.0/4"
        };
        var evaluator = new DnsAddressExclusionEvaluator(rules);
        var address = IPAddress.Parse(ipString);

        // Act
        var result = evaluator.ShouldExclude(address);

        // Assert
        result.Should().Be(expectedResult);
    }

    [Fact]
    public void ShouldExclude_WhenRulesAreNull_ReturnsFalse()
    {
        // Arrange
        var evaluator = new DnsAddressExclusionEvaluator(null);
        var address = IPAddress.Parse("169.254.1.1");

        // Act
        var result = evaluator.ShouldExclude(address);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldExclude_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var evaluator = new DnsAddressExclusionEvaluator([]);

        // Act
        Action act = () => evaluator.ShouldExclude(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
