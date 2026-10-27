using AwesomeAssertions;
using NetFlowAudit.Core.Export;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class FirewallRuleExporterTests : IDisposable
{
    private readonly string _tempDirectory;

    public FirewallRuleExporterTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "NetFlowAudit_RuleExportTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, recursive: true);
            }
            catch (IOException)
            {
                // Tolerated in cleanup
            }
        }
    }

    [Fact]
    public async Task ExportRulesAsync_WhenValidCsvInput_GeneratesFormattedFirewallMatrixFile()
    {
        // Arrange
        var auditCsv = Path.Combine(_tempDirectory, "audit.csv");
        var header = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";
        var row1 = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,10,SRV-APP-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.20.1.5,1521,db-oracle.prod,1234,tnslsnr,/usr/bin/tnslsnr,tnslsnr --run,,oracle";
        var row2 = "2026-10-24T07:01:00Z,2026-10-24T07:06:00Z,25,SRV-APP-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50002,10.20.1.5,1521,db-oracle.prod,1234,tnslsnr,/usr/bin/tnslsnr,tnslsnr --run,,oracle";
        await File.WriteAllLinesAsync(auditCsv, [header, row1, row2]);

        var targetMatrixCsv = Path.Combine(_tempDirectory, "firewall_matrix.csv");
        var exporter = new FirewallRuleExporter();
        var options = new FirewallRuleExportOptions();

        // Act
        var report = await exporter.ExportRulesAsync([auditCsv], targetMatrixCsv, options);

        // Assert
        report.TotalRawFlowsScanned.Should().Be(2);
        report.TotalRulesExported.Should().Be(1);
        report.DestinationCsvPath.Should().Be(targetMatrixCsv);

        File.Exists(targetMatrixCsv).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(targetMatrixCsv);
        lines.Should().HaveCount(2); // Header + 1 consolidated rule
        lines[0].Should().StartWith("RuleId,Direction,SourceIp,");
        lines[1].Should().StartWith("FLUX-001,Outbound,10.0.0.1,SRV-APP-01,10.20.1.5,db-oracle.prod,Internal,TCP,1521,Oracle-TNS,tnslsnr,");
        lines[1].Should().Contain(",35,"); // Sum of observation count (10 + 25)
    }
}
