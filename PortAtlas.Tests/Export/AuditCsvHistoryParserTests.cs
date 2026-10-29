using System.Net;
using AwesomeAssertions;
using PortAtlas.Core.Export;
using PortAtlas.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace PortAtlas.Tests.Export;

public sealed class AuditCsvHistoryParserTests : IDisposable
{
    private readonly string _tempDirectory;

    public AuditCsvHistoryParserTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "netflow_history_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenFileNotFound_ReturnsEmptyList()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var nonExistentPath = Path.Combine(_tempDirectory, "does_not_exist.csv");

        // Act
        var entries = await parser.ParseHistoryAsync(nonExistentPath, maxRows: null, delimiter: ',');

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenEmptyFile_ReturnsEmptyList()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var emptyPath = Path.Combine(_tempDirectory, "empty.csv");
        await File.WriteAllTextAsync(emptyPath, string.Empty);

        // Act
        var entries = await parser.ParseHistoryAsync(emptyPath, maxRows: null, delimiter: ',');

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenHeaderOnly_ReturnsEmptyList()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var headerOnlyPath = Path.Combine(_tempDirectory, "header_only.csv");
        await File.WriteAllTextAsync(headerOnlyPath, "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,Protocol,LocalPort,RemoteIp,RemotePort,ProcessName\n");

        // Act
        var entries = await parser.ParseHistoryAsync(headerOnlyPath, maxRows: null, delimiter: ',');

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenMaxRowsIsZero_ReturnsEmptyList()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var csvPath = Path.Combine(_tempDirectory, "valid.csv");
        var csvContent = """
            FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName
            2026-10-24T07:00:00.0000000Z,2026-10-24T07:05:00.0000000Z,10,SRV-APP-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,1.1.1.1,443,one.one.one.one,1234,curl,/usr/bin/curl,curl https://1.1.1.1,,appuser
            """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var entries = await parser.ParseHistoryAsync(csvPath, maxRows: 0, delimiter: ',');

        // Assert
        entries.Should().BeEmpty();
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenValidCsvWithStandardHeader_ParsesFieldsAccurately()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var csvPath = Path.Combine(_tempDirectory, "two_rows.csv");
        var csvContent = """
            FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName
            2026-10-24T07:00:00.0000000Z,2026-10-24T07:05:00.0000000Z,15,SRV-01,Windows,192.168.1.10,Tcp,Outbound,Established,192.168.1.10,54321,172.217.119.4,443,google.com,4321,chrome,C:\Chrome\chrome.exe,chrome.exe https://google.com,,user1
            2026-10-24T07:01:00.0000000Z,2026-10-24T07:06:00.0000000Z,5,SRV-01,Windows,192.168.1.10,Tcp,Listening,Listen,0.0.0.0,8080,0.0.0.0,0,,9999,java,C:\Java\java.exe,java -jar app.jar,app.jar,appservice
            """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var entries = await parser.ParseHistoryAsync(csvPath, maxRows: null, delimiter: ',');

        // Assert
        entries.Should().HaveCount(2);

        var first = entries[0];
        first.Server.HostName.Should().Be("SRV-01");
        first.Server.OperatingSystemDescription.Should().Be("Windows");
        first.Server.PrimaryIpAddress.Should().Be("192.168.1.10");
        first.ObservationCount.Should().Be(15);
        first.Socket.Protocol.Should().Be(SocketTransportProtocol.Tcp);
        first.Socket.Direction.Should().Be(SocketFlowDirection.Outbound);
        first.Socket.LocalEndpoint.Port.Should().Be(54321);
        first.Socket.RemoteEndpoint.Address.Should().Be(IPAddress.Parse("172.217.119.4"));
        first.Socket.RemoteEndpoint.Port.Should().Be(443);
        first.RemoteHostName.Should().Be("google.com");
        first.Socket.Process.ProcessName.Should().Be("chrome");

        var second = entries[1];
        second.Socket.Direction.Should().Be(SocketFlowDirection.Listening);
        second.Socket.LocalEndpoint.Port.Should().Be(8080);
        second.Socket.Process.TargetExecutable.Should().Be("app.jar");
        second.Socket.Process.UserName.Should().Be("appservice");
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenMaxRowsSpecified_ReturnsOnlyMostRecentNEntries()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var csvPath = Path.Combine(_tempDirectory, "three_rows.csv");
        var csvContent = """
            FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName
            2026-10-24T07:00:00.0000000Z,2026-10-24T07:00:00.0000000Z,1,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,1001,1.1.1.1,80,,1,p1,,,,,
            2026-10-24T07:01:00.0000000Z,2026-10-24T07:01:00.0000000Z,1,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,1002,1.1.1.2,80,,2,p2,,,,,
            2026-10-24T07:02:00.0000000Z,2026-10-24T07:02:00.0000000Z,1,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,1003,1.1.1.3,80,,3,p3,,,,,
            """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act (request last 2 rows)
        var entries = await parser.ParseHistoryAsync(csvPath, maxRows: 2, delimiter: ',');

        // Assert
        entries.Should().HaveCount(2);
        entries[0].Socket.Process.ProcessName.Should().Be("p2");
        entries[1].Socket.Process.ProcessName.Should().Be("p3");
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenHeaderMissing_UsesStandardPositionalMapping()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var csvPath = Path.Combine(_tempDirectory, "no_header.csv");
        // No header line, straight 20 columns data
        var csvContent = "2026-10-24T07:00:00.0000000Z,2026-10-24T07:05:00.0000000Z,3,SRV-HEADLESS,Linux,10.0.0.5,Tcp,Outbound,Established,10.0.0.5,45000,8.8.8.8,53,dns.google,555,dig,/usr/bin/dig,dig @8.8.8.8,,root\n";
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var entries = await parser.ParseHistoryAsync(csvPath, maxRows: null, delimiter: ',');

        // Assert
        entries.Should().HaveCount(1);
        entries[0].Server.HostName.Should().Be("SRV-HEADLESS");
        entries[0].Socket.RemoteEndpoint.Address.Should().Be(IPAddress.Parse("8.8.8.8"));
        entries[0].Socket.RemoteEndpoint.Port.Should().Be(53);
        entries[0].RemoteHostName.Should().Be("dns.google");
        entries[0].Socket.Process.ProcessName.Should().Be("dig");
    }

    [Fact]
    public async Task ParseHistoryAsync_WhenValuesContainEscapedQuotesAndDelimiters_ParsesAccurately()
    {
        // Arrange
        var parser = new AuditCsvHistoryParser();
        var csvPath = Path.Combine(_tempDirectory, "escaped.csv");
        var csvContent = """
            FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName
            2026-10-24T07:00:00.0000000Z,2026-10-24T07:05:00.0000000Z,1,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.0.0.2,443,,1234,runner,"/opt/app/my runner.sh","runner --arg ""quoted,value"" --flag",app.sh,"DOMAIN\User, Special"
            """;
        await File.WriteAllTextAsync(csvPath, csvContent);

        // Act
        var entries = await parser.ParseHistoryAsync(csvPath, maxRows: null, delimiter: ',');

        // Assert
        entries.Should().HaveCount(1);
        var entry = entries[0];
        entry.Socket.Process.ExecutablePath.Should().Be("/opt/app/my runner.sh");
        entry.Socket.Process.CommandLine.Should().Be("runner --arg \"quoted,value\" --flag");
        entry.Socket.Process.UserName.Should().Be("DOMAIN\\User, Special");
    }
}
