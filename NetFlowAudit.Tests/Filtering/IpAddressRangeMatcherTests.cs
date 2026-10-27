using System.Net;
using AwesomeAssertions;
using NetFlowAudit.Core.Filtering;
using Xunit;

namespace NetFlowAudit.Tests.Filtering;

public sealed class IpAddressRangeMatcherTests
{
    [Fact]
    public void IsMatch_WhenAddressMatchesExactIp_ReturnsTrue()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["192.168.1.50", "8.8.8.8"]);
        var address = IPAddress.Parse("8.8.8.8");

        // Act & Assert
        matcher.IsMatch(address).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenAddressMatchesCidr_ReturnsTrue()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["10.0.0.0/8", "172.16.0.0/12"]);
        var address = IPAddress.Parse("10.254.1.2");

        // Act & Assert
        matcher.IsMatch(address).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenAddressDoesNotMatch_ReturnsFalse()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["10.0.0.0/8", "192.168.1.1"]);
        var address = IPAddress.Parse("142.250.74.206");

        // Act & Assert
        matcher.IsMatch(address).Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WhenRulesAreNull_ReturnsFalse()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(null);
        var address = IPAddress.Parse("1.1.1.1");

        // Act & Assert
        matcher.IsMatch(address).Should().BeFalse();
        matcher.HasRules.Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WhenRulesContainInvalidOrEmptyStrings_IgnoresThem()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["", "   ", "invalid-ip", "not-a-cidr/abc"]);
        var address = IPAddress.Parse("1.1.1.1");

        // Act & Assert
        matcher.IsMatch(address).Should().BeFalse();
        matcher.HasRules.Should().BeFalse();
    }

    [Fact]
    public void IsMatch_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["10.0.0.0/8"]);

        // Act & Assert
        var act = () => matcher.IsMatch(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void HasRules_WhenRulesAreProvided_ReturnsTrue()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["192.168.1.1"]);

        // Act & Assert
        matcher.HasRules.Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenAddressIsIPv4MappedToIPv6_MatchesIPv4Rule()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["52.123.242.181"]);
        var mappedAddress = IPAddress.Parse("::ffff:52.123.242.181");

        // Act & Assert
        matcher.IsMatch(mappedAddress).Should().BeTrue();
    }

    [Fact]
    public void IsMatch_WhenRuleIsIPv4MappedToIPv6_MatchesPureIPv4Address()
    {
        // Arrange
        var matcher = new IpAddressRangeMatcher(["::ffff:52.123.242.181"]);
        var address = IPAddress.Parse("52.123.242.181");

        // Act & Assert
        matcher.IsMatch(address).Should().BeTrue();
    }
}
