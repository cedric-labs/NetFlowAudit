using System.Net;
using System.Text.Json;
using AwesomeAssertions;
using PortAtlas.Core.Export;
using PortAtlas.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace PortAtlas.Tests.Export;

public sealed class AuditNdjsonFormatterTests
{
    private readonly AuditNdjsonFormatter _formatter = new();

    [Fact]
    public void FormatEntry_WhenEntryIsValid_ReturnsSingleLineJsonWithCamelCaseKeys()
    {
        // Arrange
        var entry = CreateSampleEntry();

        // Act
        var ndjsonLine = _formatter.FormatEntry(entry);

        // Assert
        ndjsonLine.Should().NotBeNullOrWhiteSpace();
        ndjsonLine.Should().NotContain("\r");
        ndjsonLine.Should().NotContain("\n");

        using var document = JsonDocument.Parse(ndjsonLine);
        var root = document.RootElement;

        root.GetProperty("serverName").GetString().Should().Be("SRV-PROD-01");
        root.GetProperty("serverOsVersion").GetString().Should().Be("Linux 6.8.0");
        root.GetProperty("serverPrimaryIp").GetString().Should().Be("192.168.1.10");
        root.GetProperty("protocol").GetString().Should().Be("Tcp");
        root.GetProperty("direction").GetString().Should().Be("Outbound");
        root.GetProperty("tcpState").GetString().Should().Be("ESTABLISHED");
        root.GetProperty("localIp").GetString().Should().Be("192.168.1.10");
        root.GetProperty("localPort").GetInt32().Should().Be(54321);
        root.GetProperty("remoteIp").GetString().Should().Be("93.184.216.34");
        root.GetProperty("remotePort").GetInt32().Should().Be(443);
        root.GetProperty("remoteHostName").GetString().Should().Be("example.com");
        root.GetProperty("processId").GetInt32().Should().Be(4242);
        root.GetProperty("processName").GetString().Should().Be("curl");
        root.GetProperty("executablePath").GetString().Should().Be("/usr/bin/curl");
        root.GetProperty("commandLine").GetString().Should().Be("curl https://example.com");
        root.GetProperty("targetExecutable").GetString().Should().Be("/usr/bin/curl");
        root.GetProperty("userName").GetString().Should().Be("appuser");
        root.GetProperty("observationCount").GetInt64().Should().Be(5);
    }

    [Fact]
    public void FormatEntry_WhenEntryIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _formatter.FormatEntry(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void FormatRecord_WhenRecordIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => _formatter.FormatRecord(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    private static NetworkAuditEntry CreateSampleEntry()
    {
        var firstSeen = new DateTimeOffset(2026, 10, 25, 10, 0, 0, TimeSpan.Zero);
        var lastSeen = new DateTimeOffset(2026, 10, 25, 10, 5, 0, TimeSpan.Zero);

        var server = new ServerHostMetadata("SRV-PROD-01", "Linux 6.8.0", "192.168.1.10");
        var process = new ProcessMetadata(4242, "curl", "/usr/bin/curl", "curl https://example.com", "/usr/bin/curl", "appuser");
        var socket = new ActiveSocketConnection(
            Protocol: SocketTransportProtocol.Tcp,
            Direction: SocketFlowDirection.Outbound,
            LocalEndpoint: new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 54321),
            RemoteEndpoint: new SocketEndpoint(IPAddress.Parse("93.184.216.34"), 443),
            State: "ESTABLISHED",
            Process: process);

        return new NetworkAuditEntry(firstSeen, lastSeen, 5, server, socket, "example.com");
    }
}
