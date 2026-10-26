using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class FirewallRuleAggregatorTests
{
    [Fact]
    public void Aggregate_WhenOutboundFlow_NormalizesSourceAndDestinationCorrectly()
    {
        // Arrange
        var aggregator = new FirewallRuleAggregator();
        var entry = CreateSampleEntry(
            direction: SocketFlowDirection.Outbound,
            localIp: "10.0.0.10",
            localPort: 52140,
            remoteIp: "10.20.1.5",
            remotePort: 1521,
            serverHost: "SRV-APP-01",
            remoteHost: "db-oracle.prod",
            processName: "tnslsnr",
            observationCount: 150);

        var options = new FirewallRuleExportOptions();

        // Act
        var rules = aggregator.Aggregate([entry], options);

        // Assert
        rules.Should().HaveCount(1);
        var rule = rules[0];
        rule.RuleId.Should().Be("FLUX-001");
        rule.Direction.Should().Be("Outbound");
        rule.SourceIp.Should().Be("10.0.0.10");
        rule.SourceHost.Should().Be("SRV-APP-01");
        rule.DestinationIp.Should().Be("10.20.1.5");
        rule.DestinationHost.Should().Be("db-oracle.prod");
        rule.DestinationPort.Should().Be(1521);
        rule.DestinationZone.Should().Be("Internal");
        rule.ServiceName.Should().Be("Oracle-TNS");
        rule.ProcessName.Should().Be("tnslsnr");
        rule.ObservationCount.Should().Be(150);
    }

    [Fact]
    public void Aggregate_WhenInboundFlow_InvertsSourceAndDestinationToMatchRealTraffic()
    {
        // Arrange
        var aggregator = new FirewallRuleAggregator();
        var entry = CreateSampleEntry(
            direction: SocketFlowDirection.Inbound,
            localIp: "10.0.0.10",
            localPort: 443,
            remoteIp: "192.168.1.100",
            remotePort: 60123,
            serverHost: "SRV-WEB-01",
            remoteHost: "client.local",
            processName: "nginx",
            observationCount: 50);

        var options = new FirewallRuleExportOptions();

        // Act
        var rules = aggregator.Aggregate([entry], options);

        // Assert
        rules.Should().HaveCount(1);
        var rule = rules[0];
        rule.Direction.Should().Be("Inbound");
        rule.SourceIp.Should().Be("192.168.1.100");
        rule.SourceHost.Should().Be("client.local");
        rule.DestinationIp.Should().Be("10.0.0.10");
        rule.DestinationHost.Should().Be("SRV-WEB-01");
        rule.DestinationPort.Should().Be(443);
        rule.ServiceName.Should().Be("HTTPS");
    }

    [Fact]
    public void Aggregate_WhenMultipleEntriesShareSameTuple_ConsolidatesObservationsAndDates()
    {
        // Arrange
        var aggregator = new FirewallRuleAggregator();
        var time1 = DateTimeOffset.UtcNow.AddHours(-3);
        var time2 = DateTimeOffset.UtcNow.AddHours(-1);

        var entry1 = CreateSampleEntry(
            direction: SocketFlowDirection.Outbound,
            localIp: "10.0.0.10",
            localPort: 50001,
            remoteIp: "10.20.1.5",
            remotePort: 443,
            firstSeen: time1,
            lastSeen: time1.AddMinutes(10),
            observationCount: 20);

        var entry2 = CreateSampleEntry(
            direction: SocketFlowDirection.Outbound,
            localIp: "10.0.0.10",
            localPort: 50002,
            remoteIp: "10.20.1.5",
            remotePort: 443,
            firstSeen: time2,
            lastSeen: time2.AddMinutes(5),
            observationCount: 30);

        var options = new FirewallRuleExportOptions();

        // Act
        var rules = aggregator.Aggregate([entry1, entry2], options);

        // Assert
        rules.Should().HaveCount(1);
        var rule = rules[0];
        rule.ObservationCount.Should().Be(50);
        rule.FirstSeenUtc.Should().Be(time1);
        rule.LastSeenUtc.Should().Be(time2.AddMinutes(5));
    }

    [Fact]
    public void Aggregate_WhenDestinationPortIsEphemeralAndExcludeEphemeralEnabled_FiltersRuleOut()
    {
        // Arrange
        var aggregator = new FirewallRuleAggregator();
        var entry = CreateSampleEntry(
            direction: SocketFlowDirection.Outbound,
            localIp: "10.0.0.10",
            localPort: 45000,
            remoteIp: "10.20.1.5",
            remotePort: 55000); // Ephemeral destination port

        var options = new FirewallRuleExportOptions { ExcludeEphemeralPorts = true };

        // Act
        var rules = aggregator.Aggregate([entry], options);

        // Assert
        rules.Should().BeEmpty();
    }

    [Fact]
    public void Aggregate_WhenObservationCountBelowThreshold_FiltersRuleOut()
    {
        // Arrange
        var aggregator = new FirewallRuleAggregator();
        var entry = CreateSampleEntry(
            direction: SocketFlowDirection.Outbound,
            localIp: "10.0.0.10",
            localPort: 50000,
            remoteIp: "10.20.1.5",
            remotePort: 443,
            observationCount: 3);

        var options = new FirewallRuleExportOptions { MinObservations = 5 };

        // Act
        var rules = aggregator.Aggregate([entry], options);

        // Assert
        rules.Should().BeEmpty();
    }

    [Fact]
    public void Aggregate_WhenInternalOnlyFilterActiveAndTargetIsPublic_FiltersRuleOut()
    {
        // Arrange
        var aggregator = new FirewallRuleAggregator();
        var internalEntry = CreateSampleEntry(
            direction: SocketFlowDirection.Outbound,
            localIp: "10.0.0.10",
            localPort: 50000,
            remoteIp: "10.20.1.5",
            remotePort: 443);

        var publicEntry = CreateSampleEntry(
            direction: SocketFlowDirection.Outbound,
            localIp: "10.0.0.10",
            localPort: 50001,
            remoteIp: "8.8.8.8",
            remotePort: 53);

        var options = new FirewallRuleExportOptions { DestinationZoneFilter = "internal" };

        // Act
        var rules = aggregator.Aggregate([internalEntry, publicEntry], options);

        // Assert
        rules.Should().HaveCount(1);
        rules[0].DestinationIp.Should().Be("10.20.1.5");
    }

    private static NetworkAuditEntry CreateSampleEntry(
        SocketFlowDirection direction,
        string localIp,
        int localPort,
        string remoteIp,
        int remotePort,
        string serverHost = "SRV-TEST",
        string remoteHost = "",
        string processName = "testproc",
        long observationCount = 10,
        DateTimeOffset? firstSeen = null,
        DateTimeOffset? lastSeen = null)
    {
        var now = DateTimeOffset.UtcNow;
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            direction,
            new SocketEndpoint(IPAddress.Parse(localIp), localPort),
            new SocketEndpoint(IPAddress.Parse(remoteIp), remotePort),
            "Established",
            new ProcessMetadata(1234, processName, $"/usr/bin/{processName}", $"{processName} --run", "", "root"));

        return new NetworkAuditEntry(
            FirstSeenUtc: firstSeen ?? now.AddHours(-1),
            LastSeenUtc: lastSeen ?? now,
            ObservationCount: observationCount,
            Server: new ServerHostMetadata(serverHost, "Linux", localIp),
            Socket: socket,
            RemoteHostName: remoteHost);
    }
}
