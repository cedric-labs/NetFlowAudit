using AwesomeAssertions;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Export;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class FirewallRuleExportCommandRunnerTests : IDisposable
{
    private readonly string _tempDirectory;

    public FirewallRuleExportCommandRunnerTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "NetFlowAudit_CmdExportTests_" + Guid.NewGuid().ToString("N"));
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
    public async Task RunAsync_WhenInvokedWithValidAudit_GeneratesFirewallMatrixCsv()
    {
        // Arrange
        var auditCsv = Path.Combine(_tempDirectory, "audit.csv");
        var header = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";
        var row = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,10,SRV-APP-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.20.1.5,1521,db-oracle.prod,1234,tnslsnr,/usr/bin/tnslsnr,tnslsnr --run,,oracle";
        await File.WriteAllLinesAsync(auditCsv, [header, row]);

        var targetMatrix = Path.Combine(_tempDirectory, "rules.csv");
        var config = new AppConfiguration();
        var args = new[]
        {
            "--export-rules", targetMatrix,
            "-i", auditCsv
        };

        var runner = new FirewallRuleExportCommandRunner();

        // Act
        var exitCode = await runner.RunAsync(config, args);

        // Assert
        exitCode.Should().Be(0);
        File.Exists(targetMatrix).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(targetMatrix);
        lines.Should().HaveCount(2);
        lines[1].Should().StartWith("FLUX-001,Outbound,10.0.0.1,SRV-APP-01,10.20.1.5,db-oracle.prod,Internal,TCP,1521,Oracle-TNS,");
    }
}
