using AwesomeAssertions;
using PortAtlas.Core.Export;
using Xunit;

namespace PortAtlas.Tests.Export;

public sealed class FirewallRulePairAggregatorTests
{
    [Fact]
    public void Aggregate_WhenRulesShareSourceAndDestination_MergesThemIntoOnePairPerDirection()
    {
        // Arrange
        var rules = new[]
        {
            Rule("192.168.1.10", "desktop", "192.168.1.50", "nas", 443, 5, "2026-10-02T08:00:00Z", "2026-10-03T08:00:00Z"),
            Rule("192.168.1.10", "desktop", "192.168.1.50", "nas", 873, 7, "2026-10-01T08:00:00Z", "2026-10-02T08:00:00Z"),
            Rule("192.168.1.10", "desktop", "192.168.1.50", "nas", 443, 3, "2026-10-02T09:00:00Z", "2026-10-04T08:00:00Z"),
            Rule("192.168.1.50", "nas", "192.168.1.10", "desktop", 8080, 2, "2026-10-02T08:00:00Z", "2026-10-02T09:00:00Z")
        };

        // Act
        var pairs = new FirewallRulePairAggregator().Aggregate(rules);

        // Assert
        pairs.Should().HaveCount(2);
        var desktopToNas = pairs[0];
        desktopToNas.PairId.Should().Be("PAIR-001");
        desktopToNas.SourceHost.Should().Be("desktop");
        desktopToNas.DestinationHost.Should().Be("nas");
        desktopToNas.FlowCount.Should().Be(3);
        desktopToNas.Ports.Should().Equal("443/TCP", "873/TCP");
        desktopToNas.ObservationCount.Should().Be(15);
        desktopToNas.FirstSeenUtc.Should().Be(DateTimeOffset.Parse("2026-10-01T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        desktopToNas.LastSeenUtc.Should().Be(DateTimeOffset.Parse("2026-10-04T08:00:00Z", System.Globalization.CultureInfo.InvariantCulture));
        pairs[1].PairId.Should().Be("PAIR-002");
        pairs[1].SourceHost.Should().Be("nas");
        pairs[1].Ports.Should().Equal("8080/TCP");
    }

    private static FirewallRuleItem Rule(
        string sourceIp, string sourceHost, string destinationIp, string destinationHost, int port, long observations, string firstSeen, string lastSeen) => new()
        {
            RuleId = "FLUX-000",
            Direction = "Outbound",
            SourceIp = sourceIp,
            SourceHost = sourceHost,
            DestinationIp = destinationIp,
            DestinationHost = destinationHost,
            DestinationZone = "Internal",
            Protocol = "TCP",
            DestinationPort = port,
            ServiceName = "svc",
            ProcessName = "proc",
            ExecutablePath = string.Empty,
            UserName = string.Empty,
            ObservationCount = observations,
            FirstSeenUtc = DateTimeOffset.Parse(firstSeen, System.Globalization.CultureInfo.InvariantCulture),
            LastSeenUtc = DateTimeOffset.Parse(lastSeen, System.Globalization.CultureInfo.InvariantCulture)
        };
}
