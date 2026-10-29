using System.Text.Json;
using AwesomeAssertions;
using PortAtlas.Cli;
using PortAtlas.Cli.Configuration;
using PortAtlas.Core.Export;
using Xunit;

namespace PortAtlas.Tests.CommandLine;

/// <summary>
/// Pins the observable behaviour of each command (files produced from a known audit log) so that the
/// command-line parser can be replaced (ADR-0001) without changing what the commands compute.
/// </summary>
[Collection(ConsoleOutputCollection.Name)]
public sealed class CliCharacterizationTests : IDisposable
{
    private const string Header = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";
    private const string OracleRow = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,10,SRV-APP-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.20.1.5,1521,db-oracle.example.test,1234,tnslsnr,/usr/bin/tnslsnr,tnslsnr --run,,oracle";
    private const string PublicRow = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,2,SRV-APP-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50001,203.0.113.10,443,,2222,git,/usr/bin/git,git fetch,,deploy";
    private const string ListenerRow = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,7,SRV-APP-01,Linux,10.0.0.1,Tcp,Listening,Listen,10.0.0.1,8080,0.0.0.0,0,,3333,java,/usr/bin/java,java -jar web.jar,web.jar,web";
    private const string SecondServerRow = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,4,SRV-APP-02,Linux,10.0.0.2,Tcp,Outbound,Established,10.0.0.2,50002,10.20.1.5,1521,db-oracle.example.test,4444,sqlplus,/usr/bin/sqlplus,sqlplus,,oracle";

    private readonly string _directory;
    private readonly string _auditPath;
    private readonly string _missingConfigPath;

    public CliCharacterizationTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PortAtlas_CliCharacterization_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _auditPath = Path.Combine(_directory, "audit_srv1.csv");
        File.WriteAllLines(_auditPath, [Header, OracleRow, PublicRow, ListenerRow]);
        _missingConfigPath = Path.Combine(_directory, "absent-config.json");
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_directory, recursive: true);
        }
        catch (IOException)
        {
            // A file still held by the OS must not fail the test run; the folder lives in the temp directory.
        }
    }

    [Fact]
    public async Task ExportRules_WhenMinimumObservationsAndInternalZoneGiven_ExportsOnlyFrequentInternalRules()
    {
        // Arrange
        var matrix = Path.Combine(_directory, "rules.csv");

        // Act
        await RunAsync("export", "rules", "-o", matrix, "-i", _auditPath, "--min-observations", "5", "--zone", "internal");

        // Assert
        var rows = (await File.ReadAllLinesAsync(matrix)).Skip(1).ToList();
        rows.Should().ContainSingle().Which.Should().StartWith("FLUX-001,Outbound,10.0.0.1,SRV-APP-01,10.20.1.5,db-oracle.example.test,Internal,TCP,1521,Oracle-TNS,");
    }

    [Fact]
    public async Task ExportRules_WhenPairsOutputGiven_WritesThePairMatrixWithTheSameFilters()
    {
        // Arrange
        var matrix = Path.Combine(_directory, "rules_detailed.csv");
        var pairs = Path.Combine(_directory, "rules_pairs.csv");

        // Act
        await RunAsync("export", "rules", "-o", matrix, "-i", _auditPath, "--zone", "internal", "--pairs-output", pairs);

        // Assert
        File.Exists(matrix).Should().BeTrue();
        var rows = (await File.ReadAllLinesAsync(pairs)).Skip(1).ToList();
        rows.Should().ContainSingle().Which.Should().StartWith("PAIR-001,10.0.0.1,SRV-APP-01,10.20.1.5,db-oracle.example.test,");
    }

    [Fact]
    public async Task ExportRules_WhenPublicZoneGiven_ExportsOnlyPublicDestinations()
    {
        // Arrange
        var matrix = Path.Combine(_directory, "rules_public.csv");

        // Act
        await RunAsync("export", "rules", "-o", matrix, "-i", _auditPath, "--zone", "public");

        // Assert
        var rows = (await File.ReadAllLinesAsync(matrix)).Skip(1).ToList();
        rows.Should().ContainSingle().Which.Should().Contain(",203.0.113.10,");
    }

    [Fact]
    public async Task ExportRules_WhenListenersOnlyGiven_ExportsOneInboundRulePerListeningService()
    {
        // Arrange
        var matrix = Path.Combine(_directory, "rules_listeners.csv");

        // Act
        await RunAsync("export", "rules", "-o", matrix, "-i", _auditPath, "--listeners-only");

        // Assert
        (await File.ReadAllLinesAsync(matrix)).Skip(1).Should().ContainSingle()
            .Which.Should().StartWith("FLUX-001,Inbound,0.0.0.0,Any,10.0.0.1,SRV-APP-01,Internal,TCP,8080,");
    }

    [Fact]
    public async Task ExportNdjson_WhenCsvAuditGiven_WritesOneJsonLinePerFlow()
    {
        // Arrange
        var ndjson = Path.Combine(_directory, "audit.ndjson");

        // Act
        await RunAsync("export", "ndjson", "-o", ndjson, "-i", _auditPath);

        // Assert
        var lines = await File.ReadAllLinesAsync(ndjson);
        lines.Should().HaveCount(3);
        lines.Should().AllSatisfy(line => JsonDocument.Parse(line).RootElement.ValueKind.Should().Be(JsonValueKind.Object));
    }

    [Fact]
    public async Task Map_WhenIncludedPortGiven_RendersOnlyMatchingFlows()
    {
        // Arrange
        var html = Path.Combine(_directory, "map.html");

        // Act
        await RunAsync("map", "-i", _auditPath, "--html", html, "--include-ports", "1521");

        // Assert
        var content = await File.ReadAllTextAsync(html);
        content.Should().Contain("db-oracle.example.test");
        content.Should().NotContain("203.0.113.10");
    }

    [Fact]
    public async Task Map_WhenWildcardInputGiven_ConsolidatesEveryMatchingAudit()
    {
        // Arrange
        await File.WriteAllLinesAsync(Path.Combine(_directory, "audit_srv2.csv"), [Header, SecondServerRow]);
        var html = Path.Combine(_directory, "fleet.html");

        // Act
        await RunAsync("map", "-i", Path.Combine(_directory, "audit_*.csv"), "--html", html);

        // Assert
        var content = await File.ReadAllTextAsync(html);
        content.Should().Contain("SRV-APP-01");
        content.Should().Contain("SRV-APP-02");
    }

    [Fact]
    public async Task MapDrawio_WhenOnlyDrawioOutputGiven_WritesDiagramWithoutHtmlMap()
    {
        // Arrange
        var drawio = Path.Combine(_directory, "map.drawio");

        // Act
        await RunAsync("map", "-i", _auditPath, "--drawio", drawio, "--drawio-theme", "light");

        // Assert
        File.Exists(drawio).Should().BeTrue();
        (await File.ReadAllTextAsync(drawio)).Should().Contain("<mxfile");
        Directory.GetFiles(_directory, "*.html").Should().BeEmpty();
    }

    [Fact]
    public async Task Resolve_WhenCacheOnlyGiven_EnrichesAuditFromCache()
    {
        // Arrange
        var cache = Path.Combine(_directory, "cache.json");
        await File.WriteAllTextAsync(cache, "{\"203.0.113.10\": {\"HostName\": \"git.example.test\", \"Source\": \"DNS\"}}");
        var masterCache = Path.Combine(_directory, "master_cache.json");

        // Act
        await RunAsync("resolve", "-i", _auditPath, "--cache", cache, "--output-cache", masterCache, "--cache-only");

        // Assert
        File.Exists(masterCache).Should().BeTrue();
        var enriched = await File.ReadAllLinesAsync(Path.Combine(_directory, "audit_srv1_enriched.csv"));
        enriched.Should().ContainSingle(line => line.Contains(",203.0.113.10,443,git.example.test,", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ConfigInit_WhenPathGiven_WritesDeserializableConfiguration()
    {
        // Arrange
        var target = Path.Combine(_directory, "generated.json");

        // Act
        await RunAsync("config", "init", target);

        // Assert
        var configuration = JsonSerializer.Deserialize<AppConfiguration>(await File.ReadAllTextAsync(target));
        configuration.Should().NotBeNull();
        configuration!.OutputCsvPath.Should().Be("netflow_audit.csv");
    }

    [Fact]
    public void AuditConfiguration_WhenNoRdapGiven_DisablesRdapButKeepsReverseDns()
    {
        // Act
        var configuration = AuditCommandConfiguration.Build(new AppConfiguration(), "audit", "--no-rdap");

        // Assert
        configuration.ResolveIpOwners.Should().BeFalse();
        configuration.ResolveRemoteHostNames.Should().BeTrue();
    }

    [Fact]
    public void AuditConfiguration_WhenScanAndOutputOptionsGiven_OverridesConfigurationValues()
    {
        // Act
        var configuration = AuditCommandConfiguration.Build(
            new AppConfiguration(),
            "audit", "--interval", "30", "--columns", "Protocol,RemoteIp", "--no-dns",
            "--exclude-remote-ips", "192.0.2.0/24,198.51.100.7", "--reload-history", "25", "--html", "live.html");

        // Assert
        configuration.ScanIntervalSeconds.Should().Be(30);
        configuration.CsvColumns.Should().Equal("Protocol", "RemoteIp");
        configuration.ResolveRemoteHostNames.Should().BeFalse();
        configuration.ExcludedRemoteIps.Should().Equal("192.0.2.0/24", "198.51.100.7");
        configuration.ReloadHistoryLimit.Should().Be(25);
        configuration.OutputHtmlMapPath.Should().Be("live.html");
    }

    [Fact]
    public async Task ExportRules_WhenConfigurationKeepsEphemeralMapPorts_StillExcludesEphemeralDestinations()
    {
        // Arrange
        const string ephemeralRow = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,3,SRV-APP-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50003,10.20.1.9,50555,,5555,rpc,/usr/bin/rpc,rpc,,svc";
        var audit = Path.Combine(_directory, "ephemeral.csv");
        await File.WriteAllLinesAsync(audit, [Header, OracleRow, ephemeralRow]);
        var config = await WriteConfigurationAsync(new { MapExcludeEphemeralPorts = false });
        var matrix = Path.Combine(_directory, "rules_ephemeral.csv");

        // Act
        await RunWithConfigurationAsync(config, "export", "rules", "-o", matrix, "-i", audit);

        // Assert
        (await File.ReadAllLinesAsync(matrix)).Skip(1).Should().ContainSingle().Which.Should().Contain(",1521,");
    }

    [Fact]
    public async Task Map_WhenConfigurationDefinesDrawioPath_WritesHtmlAndDrawio()
    {
        // Arrange
        var html = Path.Combine(_directory, "configured.html");
        var drawio = Path.Combine(_directory, "configured.drawio");
        var config = await WriteConfigurationAsync(new { OutputHtmlMapPath = html, OutputDrawioPath = drawio });

        // Act
        await RunWithConfigurationAsync(config, "map", "-i", _auditPath);

        // Assert
        File.Exists(html).Should().BeTrue();
        File.Exists(drawio).Should().BeTrue();
    }

    [Fact]
    public async Task Map_WhenConfiguredAuditPathContainsTokens_ReadsTheExpandedFile()
    {
        // Arrange
        var tokenizedAudit = Path.Combine(_directory, "audit_{HostName}.csv");
        File.Copy(_auditPath, OutputFilePathResolver.Resolve(tokenizedAudit));
        var html = Path.Combine(_directory, "tokens.html");
        var config = await WriteConfigurationAsync(new { OutputCsvPath = tokenizedAudit });

        // Act
        await RunWithConfigurationAsync(config, "map", "--html", html);

        // Assert
        (await File.ReadAllTextAsync(html)).Should().Contain("db-oracle.example.test");
    }

    [Fact]
    public async Task Main_WhenConfigurationEnablesRemovedIsolatedMode_ReturnsNonZeroExitCode()
    {
        // Arrange
        var config = await WriteConfigurationAsync(new { IsolatedMode = true });

        // Act
        var exitCode = await Program.Main(["export", "ndjson", "-i", _auditPath, "-o", Path.Combine(_directory, "x.ndjson"), "--config", config]);

        // Assert
        exitCode.Should().NotBe(0);
    }

    private Task RunAsync(params string[] commandArguments) =>
        RunWithConfigurationAsync(_missingConfigPath, commandArguments);

    private static async Task RunWithConfigurationAsync(string configPath, params string[] commandArguments)
    {
        var exitCode = await Program.Main([.. commandArguments, "--config", configPath]);
        exitCode.Should().Be(0);
    }

    private async Task<string> WriteConfigurationAsync(object values)
    {
        var path = Path.Combine(_directory, "portatlas.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(values));
        return path;
    }
}
