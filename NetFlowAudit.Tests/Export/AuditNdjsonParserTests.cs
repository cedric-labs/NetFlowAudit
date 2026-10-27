using System.Net;
using AwesomeAssertions;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class AuditNdjsonParserTests
{
    private readonly AuditNdjsonParser _parser = new();

    [Fact]
    public void ParseLine_WhenJsonIsValid_ReturnsMatchingNetworkAuditEntry()
    {
        // Arrange
        var json = """{"firstSeenUtc":"2026-10-25T10:00:00+00:00","lastSeenUtc":"2026-10-25T10:05:00+00:00","observationCount":5,"serverName":"SRV-PROD-01","serverOsVersion":"Linux 6.8.0","serverPrimaryIp":"192.168.1.10","protocol":"Tcp","direction":"Outbound","tcpState":"ESTABLISHED","localIp":"192.168.1.10","localPort":54321,"remoteIp":"93.184.216.34","remotePort":443,"remoteHostName":"example.com","processId":4242,"processName":"curl","executablePath":"/usr/bin/curl","commandLine":"curl https://example.com","targetExecutable":"/usr/bin/curl","userName":"appuser"}""";

        // Act
        var entry = _parser.ParseLine(json);

        // Assert
        entry.Should().NotBeNull();
        entry!.Server.HostName.Should().Be("SRV-PROD-01");
        entry.Server.OperatingSystemDescription.Should().Be("Linux 6.8.0");
        entry.Server.PrimaryIpAddress.Should().Be("192.168.1.10");
        entry.Socket.Protocol.Should().Be(SocketTransportProtocol.Tcp);
        entry.Socket.Direction.Should().Be(SocketFlowDirection.Outbound);
        entry.Socket.State.Should().Be("ESTABLISHED");
        entry.Socket.LocalEndpoint.Address.Should().Be(IPAddress.Parse("192.168.1.10"));
        entry.Socket.LocalEndpoint.Port.Should().Be(54321);
        entry.Socket.RemoteEndpoint.Address.Should().Be(IPAddress.Parse("93.184.216.34"));
        entry.Socket.RemoteEndpoint.Port.Should().Be(443);
        entry.RemoteHostName.Should().Be("example.com");
        entry.Socket.Process.ProcessId.Should().Be(4242);
        entry.Socket.Process.ProcessName.Should().Be("curl");
        entry.Socket.Process.ExecutablePath.Should().Be("/usr/bin/curl");
        entry.Socket.Process.CommandLine.Should().Be("curl https://example.com");
        entry.Socket.Process.TargetExecutable.Should().Be("/usr/bin/curl");
        entry.Socket.Process.UserName.Should().Be("appuser");
        entry.ObservationCount.Should().Be(5);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void ParseLine_WhenLineIsBlank_ReturnsNull(string line)
    {
        // Act
        var entry = _parser.ParseLine(line);

        // Assert
        entry.Should().BeNull();
    }

    [Fact]
    public void ParseLine_WhenJsonIsMalformed_ReturnsNull()
    {
        // Arrange
        var malformedJson = "{ invalid json content: 123";

        // Act
        var entry = _parser.ParseLine(malformedJson);

        // Assert
        entry.Should().BeNull();
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenFileDoesNotExist_ReturnsEmptyList()
    {
        // Act
        var result = await _parser.ParseHistoryAsync("non_existent_file.ndjson");

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenMaxRowsSpecified_ReturnsLastNEntries()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"ndjson_test_{Guid.NewGuid():N}.ndjson");
        try
        {
            var line1 = """{"firstSeenUtc":"2026-10-25T10:00:00+00:00","lastSeenUtc":"2026-10-25T10:00:00+00:00","observationCount":1,"serverName":"SRV-1","serverOsVersion":"","serverPrimaryIp":"10.0.0.1","protocol":"Tcp","direction":"Outbound","tcpState":"ESTABLISHED","localIp":"10.0.0.1","localPort":10001,"remoteIp":"10.0.0.2","remotePort":80,"remoteHostName":"","processId":1,"processName":"app","executablePath":"","commandLine":"","targetExecutable":"","userName":""}""";
            var line2 = """{"firstSeenUtc":"2026-10-25T10:01:00+00:00","lastSeenUtc":"2026-10-25T10:01:00+00:00","observationCount":2,"serverName":"SRV-2","serverOsVersion":"","serverPrimaryIp":"10.0.0.2","protocol":"Tcp","direction":"Inbound","tcpState":"LISTEN","localIp":"10.0.0.2","localPort":80,"remoteIp":"0.0.0.0","remotePort":0,"remoteHostName":"","processId":2,"processName":"nginx","executablePath":"","commandLine":"","targetExecutable":"","userName":""}""";
            var line3 = """{"firstSeenUtc":"2026-10-25T10:02:00+00:00","lastSeenUtc":"2026-10-25T10:02:00+00:00","observationCount":3,"serverName":"SRV-3","serverOsVersion":"","serverPrimaryIp":"10.0.0.3","protocol":"Tcp","direction":"Outbound","tcpState":"ESTABLISHED","localIp":"10.0.0.3","localPort":10003,"remoteIp":"10.0.0.4","remotePort":443,"remoteHostName":"","processId":3,"processName":"curl","executablePath":"","commandLine":"","targetExecutable":"","userName":""}""";

            await File.WriteAllLinesAsync(tempFile, [line1, line2, line3]);

            // Act
            var entries = await _parser.ParseHistoryAsync(tempFile, maxRows: 2);

            // Assert
            entries.Should().HaveCount(2);
            entries[0].Server.HostName.Should().Be("SRV-2");
            entries[1].Server.HostName.Should().Be("SRV-3");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
