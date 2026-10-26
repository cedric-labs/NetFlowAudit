using FluentAssertions;
using NetFlowAudit.Core.Export;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class FirewallRuleCsvFormatterTests
{
    [Fact]
    public void BuildHeader_WhenInvoked_ReturnsStandardFirewallMatrixHeader()
    {
        // Arrange
        var formatter = new FirewallRuleCsvFormatter();

        // Act
        var header = formatter.BuildHeader(',');

        // Assert
        header.Should().Be("RuleId,Direction,SourceIp,SourceHost,DestinationIp,DestinationHost,DestinationZone,Protocol,DestinationPort,ServiceName,ProcessName,ExecutablePath,UserName,ObservationCount,FirstSeenUtc,LastSeenUtc");
    }

    [Fact]
    public void FormatRow_WhenFieldContainsCommaOrQuotes_EscapesCorrectly()
    {
        // Arrange
        var formatter = new FirewallRuleCsvFormatter();
        var rule = new FirewallRuleItem
        {
            RuleId = "FLUX-001",
            Direction = "Outbound",
            SourceIp = "10.0.0.10",
            SourceHost = "SRV-APP-01",
            DestinationIp = "10.20.1.5",
            DestinationHost = "db,oracle\"prod",
            DestinationZone = "Internal",
            Protocol = "TCP",
            DestinationPort = 1521,
            ServiceName = "Oracle-TNS",
            ProcessName = "tnslsnr",
            ExecutablePath = @"C:\app\oracle\bin\tnslsnr.exe",
            UserName = @"DOMAIN\service,oracle",
            ObservationCount = 1500,
            FirstSeenUtc = DateTimeOffset.Parse("2026-10-01T08:00:00Z"),
            LastSeenUtc = DateTimeOffset.Parse("2026-10-25T17:30:00Z")
        };

        // Act
        var row = formatter.FormatRow(rule, ',');

        // Assert
        row.Should().Contain("\"db,oracle\"\"prod\"");
        row.Should().Contain("\"DOMAIN\\service,oracle\"");
        row.Should().StartWith("FLUX-001,Outbound,10.0.0.10,SRV-APP-01,10.20.1.5,");
    }
}
