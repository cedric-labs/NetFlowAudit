using System.Net;
using AwesomeAssertions;
using PortAtlas.Core.Export;
using PortAtlas.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace PortAtlas.Tests.Export;

public sealed class AuditCsvFormatterTests
{
    private static readonly ServerHostMetadata DummyServer = new("SRV-APP-01", "Linux 6.8.0", "10.10.10.2");

    [Fact]
    public void BuildHeaderLine_WhenDefaultColumns_ReturnsExpectedCsvHeader()
    {
        // Arrange
        var options = new AuditCsvOptions
        {
            SelectedColumns = [AuditCsvColumn.ServerName, AuditCsvColumn.Protocol, AuditCsvColumn.RemoteIp, AuditCsvColumn.RemotePort]
        };
        var formatter = new AuditCsvFormatter(options);

        // Act
        var header = formatter.BuildHeaderLine();

        // Assert
        header.Should().Be("ServerName,Protocol,RemoteIp,RemotePort");
    }

    [Fact]
    public void FormatEntry_WhenValuesContainSpecialCharacters_EscapesQuotesAndCommas()
    {
        // Arrange
        var options = new AuditCsvOptions
        {
            SelectedColumns = [AuditCsvColumn.ProcessName, AuditCsvColumn.CommandLine]
        };
        var formatter = new AuditCsvFormatter(options);

        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("10.10.10.2"), 54321),
            new SocketEndpoint(IPAddress.Parse("172.16.0.1"), 8080),
            "Established",
            new ProcessMetadata(9876, "java", @"/usr/bin/java", "java -jar \"app,with,comma.jar\" --port 8080", "app,with,comma.jar", "appuser"));

        var entry = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            socket);

        // Act
        var line = formatter.FormatEntry(entry);

        // Assert
        line.Should().Be("java,\"java -jar \"\"app,with,comma.jar\"\" --port 8080\"");
    }

    [Fact]
    public void FormatEntry_WhenRemoteHostNameIsIncluded_FormatsValueAccurately()
    {
        // Arrange
        var options = new AuditCsvOptions
        {
            SelectedColumns = [AuditCsvColumn.RemoteIp, AuditCsvColumn.RemotePort, AuditCsvColumn.RemoteHostName]
        };
        var formatter = new AuditCsvFormatter(options);

        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("10.10.10.2"), 54321),
            new SocketEndpoint(IPAddress.Parse("140.82.121.4"), 443),
            "Established",
            new ProcessMetadata(1234, "curl", @"C:\curl.exe", "curl https://github.com", "", "appuser"));

        var entry = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            socket,
            "github.com");

        // Act
        var line = formatter.FormatEntry(entry);

        // Assert
        line.Should().Be("140.82.121.4,443,github.com");
    }
}
