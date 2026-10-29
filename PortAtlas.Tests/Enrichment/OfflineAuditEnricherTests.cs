using System.Text.Json;
using AwesomeAssertions;
using PortAtlas.Core.Enrichment;
using PortAtlas.Core.Export;
using PortAtlas.Core.Resolution;
using Xunit;

namespace PortAtlas.Tests.Enrichment;

public sealed class OfflineAuditEnricherTests : IDisposable
{
    private readonly string _tempDirectory;

    public OfflineAuditEnricherTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "PortAtlas_EnricherTests_" + Guid.NewGuid().ToString("N"));
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
                // Tolerated in test cleanup
            }
        }
    }

    [Fact]
    public async Task EnrichFilesAsync_WhenHostNameIsEmpty_PopulatesFromCache()
    {
        // Arrange
        var csvPath = Path.Combine(_tempDirectory, "audit.csv");
        var header = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";
        var row = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,10,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.0.0.5,443,,1234,curl,/usr/bin/curl,curl https://10.0.0.5,,root";
        await File.WriteAllLinesAsync(csvPath, [header, row]);

        var cache = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.0.5"] = new() { HostName = "SRV-TARGET-05", Source = "DNS" }
        };

        var enricher = new OfflineAuditEnricher();
        var options = new OfflineAuditEnrichmentOptions
        {
            InPlace = false,
            ForceResolve = false,
            Isolated = true
        };

        // Act
        var report = await enricher.EnrichFilesAsync([csvPath], [cache], options);

        // Assert
        report.TotalRecordsScanned.Should().Be(1);
        report.TotalRecordsUpdated.Should().Be(1);
        report.GeneratedAuditFiles.Should().HaveCount(1);

        var enrichedFile = report.GeneratedAuditFiles[0];
        File.Exists(enrichedFile).Should().BeTrue();
        enrichedFile.Should().EndWith("audit_enriched.csv");

        var lines = await File.ReadAllLinesAsync(enrichedFile);
        lines.Should().HaveCount(2);
        lines[1].Should().Contain(",SRV-TARGET-05,");
    }

    [Fact]
    public async Task EnrichFilesAsync_WhenHostNameAlreadyPresentAndNoForce_PreservesExistingName()
    {
        // Arrange
        var csvPath = Path.Combine(_tempDirectory, "audit_existing.csv");
        var header = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";
        var row = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,10,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.0.0.5,443,PRE-EXISTING-NAME,1234,curl,/usr/bin/curl,curl https://10.0.0.5,,root";
        await File.WriteAllLinesAsync(csvPath, [header, row]);

        var cache = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.0.5"] = new() { HostName = "CACHE-NAME", Source = "DNS" }
        };

        var enricher = new OfflineAuditEnricher();
        var options = new OfflineAuditEnrichmentOptions
        {
            InPlace = false,
            ForceResolve = false,
            Isolated = true
        };

        // Act
        var report = await enricher.EnrichFilesAsync([csvPath], [cache], options);

        // Assert
        report.TotalRecordsUpdated.Should().Be(0);
        var enrichedFile = report.GeneratedAuditFiles[0];
        var lines = await File.ReadAllLinesAsync(enrichedFile);
        lines[1].Should().Contain(",PRE-EXISTING-NAME,");
    }

    [Fact]
    public async Task EnrichFilesAsync_WhenForceResolveEnabled_OverwritesExistingName()
    {
        // Arrange
        var csvPath = Path.Combine(_tempDirectory, "audit_force.csv");
        var header = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";
        var row = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,10,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.0.0.5,443,PRE-EXISTING-NAME,1234,curl,/usr/bin/curl,curl https://10.0.0.5,,root";
        await File.WriteAllLinesAsync(csvPath, [header, row]);

        var cache = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.0.5"] = new() { HostName = "OVERWRITTEN-NAME", Source = "DNS" }
        };

        var enricher = new OfflineAuditEnricher();
        var options = new OfflineAuditEnrichmentOptions
        {
            InPlace = false,
            ForceResolve = true,
            Isolated = true
        };

        // Act
        var report = await enricher.EnrichFilesAsync([csvPath], [cache], options);

        // Assert
        report.TotalRecordsUpdated.Should().Be(1);
        var enrichedFile = report.GeneratedAuditFiles[0];
        var lines = await File.ReadAllLinesAsync(enrichedFile);
        lines[1].Should().Contain(",OVERWRITTEN-NAME,");
    }

    [Fact]
    public async Task EnrichFilesAsync_WhenInPlaceIsTrue_OverwritesOriginalFileAtomically()
    {
        // Arrange
        var csvPath = Path.Combine(_tempDirectory, "audit_inplace.csv");
        var header = "FirstSeenUtc,LastSeenUtc,ObservationCount,ServerName,ServerOsVersion,ServerPrimaryIp,Protocol,Direction,TcpState,LocalIp,LocalPort,RemoteIp,RemotePort,RemoteHostName,ProcessId,ProcessName,ExecutablePath,CommandLine,TargetExecutable,UserName";
        var row = "2026-10-24T07:00:00Z,2026-10-24T07:05:00Z,10,SRV-01,Linux,10.0.0.1,Tcp,Outbound,Established,10.0.0.1,50000,10.0.0.5,443,,1234,curl,/usr/bin/curl,curl https://10.0.0.5,,root";
        await File.WriteAllLinesAsync(csvPath, [header, row]);

        var cache = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.0.5"] = new() { HostName = "INPLACE-NAME", Source = "DNS" }
        };

        var enricher = new OfflineAuditEnricher();
        var options = new OfflineAuditEnrichmentOptions
        {
            InPlace = true,
            ForceResolve = false,
            Isolated = true
        };

        // Act
        var report = await enricher.EnrichFilesAsync([csvPath], [cache], options);

        // Assert
        report.GeneratedAuditFiles.Should().ContainSingle().Which.Should().Be(csvPath);
        var lines = await File.ReadAllLinesAsync(csvPath);
        lines[1].Should().Contain(",INPLACE-NAME,");
    }

    [Fact]
    public async Task EnrichFilesAsync_WhenNdjsonFileProvided_EnrichesRemoteHostName()
    {
        // Arrange
        var ndjsonPath = Path.Combine(_tempDirectory, "audit.ndjson");
        var jsonRecord = "{\"remoteIp\":\"10.0.0.8\",\"remoteHostName\":\"\",\"localPort\":50000,\"remotePort\":443}";
        await File.WriteAllLinesAsync(ndjsonPath, [jsonRecord]);

        var cache = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.0.8"] = new() { HostName = "NDJSON-SERVER", Source = "DNS" }
        };

        var enricher = new OfflineAuditEnricher();
        var options = new OfflineAuditEnrichmentOptions
        {
            InPlace = false,
            ForceResolve = false,
            Isolated = true
        };

        // Act
        var report = await enricher.EnrichFilesAsync([ndjsonPath], [cache], options);

        // Assert
        report.TotalRecordsUpdated.Should().Be(1);
        var enrichedFile = report.GeneratedAuditFiles[0];
        enrichedFile.Should().EndWith("audit_enriched.ndjson");

        var lines = await File.ReadAllLinesAsync(enrichedFile);
        lines.Should().ContainSingle();
        using var doc = JsonDocument.Parse(lines[0]);
        doc.RootElement.GetProperty("remoteHostName").GetString().Should().Be("NDJSON-SERVER");
    }
}
