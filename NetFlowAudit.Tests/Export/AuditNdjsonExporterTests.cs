using System.Net;
using AwesomeAssertions;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class AuditNdjsonExporterTests
{
    private readonly AuditNdjsonExporter _exporter = new();

    [Fact]
    public async Task ExportEntriesAsync_WhenEntriesSupplied_WritesValidNdjsonFile()
    {
        // Arrange
        var tempFile = Path.Combine(Path.GetTempPath(), $"export_test_{Guid.NewGuid():N}.ndjson");
        try
        {
            var entries = new List<NetworkAuditEntry>
            {
                CreateSampleEntry("SRV-ALPHA", 80),
                CreateSampleEntry("SRV-BETA", 443)
            };

            // Act
            var exportedCount = await _exporter.ExportEntriesAsync(entries, tempFile);

            // Assert
            exportedCount.Should().Be(2);
            File.Exists(tempFile).Should().BeTrue();

            var lines = await File.ReadAllLinesAsync(tempFile);
            lines.Should().HaveCount(2);

            var parser = new AuditNdjsonParser();
            var entry1 = parser.ParseLine(lines[0]);
            var entry2 = parser.ParseLine(lines[1]);

            entry1.Should().NotBeNull();
            entry1!.Server.HostName.Should().Be("SRV-ALPHA");
            entry1.Socket.RemoteEndpoint.Port.Should().Be(80);

            entry2.Should().NotBeNull();
            entry2!.Server.HostName.Should().Be("SRV-BETA");
            entry2.Socket.RemoteEndpoint.Port.Should().Be(443);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public async Task ExportCsvToNdjsonAsync_WhenCsvFileSupplied_ConvertsAccuratelyToNdjson()
    {
        // Arrange
        var tempCsv = Path.Combine(Path.GetTempPath(), $"source_csv_{Guid.NewGuid():N}.csv");
        var tempNdjson = Path.Combine(Path.GetTempPath(), $"target_ndjson_{Guid.NewGuid():N}.ndjson");

        try
        {
            var csvContent = """
FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName
2026-10-25T10:00:00.0000000Z,2026-10-25T10:00:00.0000000Z,1,SRV-NODE1,Windows 11,10.0.0.5,Tcp,Outbound,ESTABLISHED,10.0.0.5,50000,10.0.0.6,8080,srv-api.internal,1024,node.exe,C:\node.exe,node server.js,server.js,DOMAIN\user
""";
            await File.WriteAllTextAsync(tempCsv, csvContent);

            // Act
            var convertedCount = await _exporter.ExportCsvToNdjsonAsync(tempCsv, tempNdjson);

            // Assert
            convertedCount.Should().Be(1);
            File.Exists(tempNdjson).Should().BeTrue();

            var lines = await File.ReadAllLinesAsync(tempNdjson);
            lines.Should().HaveCount(1);

            var parser = new AuditNdjsonParser();
            var parsed = parser.ParseLine(lines[0]);
            parsed.Should().NotBeNull();
            parsed!.Server.HostName.Should().Be("SRV-NODE1");
            parsed.Socket.LocalEndpoint.Port.Should().Be(50000);
            parsed.Socket.RemoteEndpoint.Port.Should().Be(8080);
            parsed.RemoteHostName.Should().Be("srv-api.internal");
            parsed.Socket.Process.ProcessName.Should().Be("node.exe");
        }
        finally
        {
            if (File.Exists(tempCsv))
            {
                File.Delete(tempCsv);
            }

            if (File.Exists(tempNdjson))
            {
                File.Delete(tempNdjson);
            }
        }
    }

    private static NetworkAuditEntry CreateSampleEntry(string hostName, int remotePort)
    {
        var now = DateTimeOffset.UtcNow;
        var server = new ServerHostMetadata(hostName, "Linux", "10.0.0.1");
        var process = new ProcessMetadata(100, "proc", "/bin/proc", "proc", "/bin/proc", "root");
        var socket = new ActiveSocketConnection(
            Protocol: SocketTransportProtocol.Tcp,
            Direction: SocketFlowDirection.Outbound,
            LocalEndpoint: new SocketEndpoint(IPAddress.Parse("10.0.0.1"), 40000),
            RemoteEndpoint: new SocketEndpoint(IPAddress.Parse("10.0.0.2"), remotePort),
            State: "ESTABLISHED",
            Process: process);

        return new NetworkAuditEntry(now, now, 1, server, socket);
    }
}
