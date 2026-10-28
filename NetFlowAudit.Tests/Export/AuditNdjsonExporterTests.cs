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

    private const string CsvHeader = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";

    [Fact]
    public async Task ExportCsvFilesToNdjsonAsync_WhenTwoCsvFilesSupplied_WritesEveryEntryInInputOrder()
    {
        // Arrange
        var tempCsv1 = Path.Combine(Path.GetTempPath(), $"stream_src1_{Guid.NewGuid():N}.csv");
        var tempCsv2 = Path.Combine(Path.GetTempPath(), $"stream_src2_{Guid.NewGuid():N}.csv");
        var tempNdjson = Path.Combine(Path.GetTempPath(), $"stream_target_{Guid.NewGuid():N}.ndjson");

        try
        {
            var content1 = string.Join(
                Environment.NewLine,
                CsvHeader,
                "2026-10-25T10:00:00.0000000Z,2026-10-25T10:00:00.0000000Z,1,SRV-APP-01,Linux,192.0.2.10,Tcp,Outbound,ESTABLISHED,192.0.2.10,50000,192.0.2.20,8080,srv-api.internal,1024,node.exe,C:\\node.exe,node server.js,server.js,DOMAIN\\user",
                "2026-10-25T10:00:00.0000000Z,2026-10-25T10:00:00.0000000Z,2,SRV-APP-01,Linux,192.0.2.10,Tcp,Outbound,ESTABLISHED,192.0.2.10,50001,192.0.2.21,443,srv-web.internal,1025,node.exe,C:\\node.exe,node server.js,server.js,DOMAIN\\user");

            var content2 = string.Join(
                Environment.NewLine,
                CsvHeader,
                "2026-10-25T10:00:00.0000000Z,2026-10-25T10:00:00.0000000Z,3,SRV-APP-02,Linux,192.0.2.11,Tcp,Outbound,ESTABLISHED,192.0.2.11,50002,192.0.2.22,1521,srv-db.internal,1026,oracle.exe,C:\\oracle.exe,oracle,oracle,DOMAIN\\user");

            await File.WriteAllTextAsync(tempCsv1, content1);
            await File.WriteAllTextAsync(tempCsv2, content2);

            // Act
            var exportedCount = await _exporter.ExportCsvFilesToNdjsonAsync([tempCsv1, tempCsv2], tempNdjson);

            // Assert
            exportedCount.Should().Be(3);
            File.Exists(tempNdjson).Should().BeTrue();

            var lines = await File.ReadAllLinesAsync(tempNdjson);
            lines.Should().HaveCount(3);

            var parser = new AuditNdjsonParser();
            var entry1 = parser.ParseLine(lines[0]);
            var entry2 = parser.ParseLine(lines[1]);
            var entry3 = parser.ParseLine(lines[2]);

            entry1.Should().NotBeNull();
            entry1!.Server.HostName.Should().Be("SRV-APP-01");
            entry1.Socket.RemoteEndpoint.Port.Should().Be(8080);

            entry2.Should().NotBeNull();
            entry2!.Server.HostName.Should().Be("SRV-APP-01");
            entry2.Socket.RemoteEndpoint.Port.Should().Be(443);

            entry3.Should().NotBeNull();
            entry3!.Server.HostName.Should().Be("SRV-APP-02");
            entry3.Socket.RemoteEndpoint.Port.Should().Be(1521);
        }
        finally
        {
            if (File.Exists(tempCsv1))
            {
                File.Delete(tempCsv1);
            }

            if (File.Exists(tempCsv2))
            {
                File.Delete(tempCsv2);
            }

            if (File.Exists(tempNdjson))
            {
                File.Delete(tempNdjson);
            }
        }
    }

    [Fact]
    public async Task ExportCsvFilesToNdjsonAsync_WhenOutputAlreadyExists_ReplacesItsContent()
    {
        // Arrange
        var tempCsv = Path.Combine(Path.GetTempPath(), $"stream_src_replace_{Guid.NewGuid():N}.csv");
        var tempNdjson = Path.Combine(Path.GetTempPath(), $"stream_target_replace_{Guid.NewGuid():N}.ndjson");

        try
        {
            var content = string.Join(
                Environment.NewLine,
                CsvHeader,
                "2026-10-25T10:00:00.0000000Z,2026-10-25T10:00:00.0000000Z,1,SRV-APP-01,Linux,192.0.2.10,Tcp,Outbound,ESTABLISHED,192.0.2.10,50000,192.0.2.20,8080,srv-api.internal,1024,node.exe,C:\\node.exe,node server.js,server.js,DOMAIN\\user");

            await File.WriteAllTextAsync(tempCsv, content);
            await File.WriteAllTextAsync(tempNdjson, "{\"preexisting\":\"data\"}\n{\"preexisting\":\"second_line\"}");

            // Act
            var exportedCount = await _exporter.ExportCsvFilesToNdjsonAsync([tempCsv], tempNdjson);

            // Assert
            exportedCount.Should().Be(1);
            var lines = await File.ReadAllLinesAsync(tempNdjson);
            lines.Should().HaveCount(1);
            lines[0].Should().NotContain("preexisting");

            var parser = new AuditNdjsonParser();
            var entry = parser.ParseLine(lines[0]);
            entry.Should().NotBeNull();
            entry!.Server.HostName.Should().Be("SRV-APP-01");
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

    [Fact]
    public async Task ExportCsvFilesToNdjsonAsync_WhenInputListIsEmpty_WritesEmptyFileAndReturnsZero()
    {
        // Arrange
        var tempNdjson = Path.Combine(Path.GetTempPath(), $"stream_empty_{Guid.NewGuid():N}.ndjson");

        try
        {
            // Act
            var exportedCount = await _exporter.ExportCsvFilesToNdjsonAsync(Array.Empty<string>(), tempNdjson);

            // Assert
            exportedCount.Should().Be(0);
            File.Exists(tempNdjson).Should().BeTrue();
            var lines = await File.ReadAllLinesAsync(tempNdjson);
            lines.Should().BeEmpty();
        }
        finally
        {
            if (File.Exists(tempNdjson))
            {
                File.Delete(tempNdjson);
            }
        }
    }

    [Fact]
    public Task ExportCsvFilesToNdjsonAsync_WhenInputListIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var tempNdjson = Path.Combine(Path.GetTempPath(), $"stream_null_{Guid.NewGuid():N}.ndjson");

        // Act
        var act = () => _exporter.ExportCsvFilesToNdjsonAsync(null!, tempNdjson);

        // Assert
        return act.Should().ThrowAsync<ArgumentNullException>();
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
