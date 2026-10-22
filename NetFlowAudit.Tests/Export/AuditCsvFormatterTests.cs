using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Models;
using Xunit;

namespace NetFlowAudit.Tests.Export;

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
    public void FormatRecordLine_WhenValuesContainSpecialCharacters_EscapesQuotesAndCommas()
    {
        // Arrange
        var options = new AuditCsvOptions
        {
            SelectedColumns = [AuditCsvColumn.ProcessName, AuditCsvColumn.CommandLine]
        };
        var formatter = new AuditCsvFormatter(options);

        var record = new NetworkConnectionRecord(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            NetworkTransportProtocol.Tcp,
            FlowDirection.Outbound,
            new NetworkEndpoint(IPAddress.Parse("10.10.10.2"), 54321),
            new NetworkEndpoint(IPAddress.Parse("172.16.0.1"), 8080),
            "Established",
            new ProcessMetadata(9876, "java", @"/usr/bin/java", "java -jar \"app,with,comma.jar\" --port 8080", "app,with,comma.jar", "appuser"));

        // Act
        var line = formatter.FormatRecordLine(record);

        // Assert
        line.Should().Be("java,\"java -jar \"\"app,with,comma.jar\"\" --port 8080\"");
    }
}
