using AwesomeAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

public sealed class DnsCacheConsolidatorTests
{
    [Fact]
    public void Consolidate_WhenSingleCacheProvided_ReturnsIdenticalEntries()
    {
        // Arrange
        var consolidator = new DnsCacheConsolidator();
        var cache = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["192.168.1.10"] = new()
            {
                HostName = "SRV-WEB-01",
                Source = "DNS",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            }
        };

        // Act
        var report = consolidator.Consolidate([cache]);

        // Assert
        report.TotalCachesProcessed.Should().Be(1);
        report.TotalEntriesLoaded.Should().Be(1);
        report.Conflicts.Should().BeEmpty();
        report.MergedEntries.Should().ContainKey("192.168.1.10");
        report.MergedEntries["192.168.1.10"].HostName.Should().Be("SRV-WEB-01");
    }

    [Fact]
    public void Consolidate_WhenMultipleCachesHaveSameIpAndSameHostName_KeepsLatestAndEmitsNoConflict()
    {
        // Arrange
        var consolidator = new DnsCacheConsolidator();
        var time1 = DateTimeOffset.UtcNow.AddHours(-2);
        var time2 = DateTimeOffset.UtcNow.AddMinutes(-5);

        var cache1 = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.0.5"] = new() { HostName = "SRV-SQL-01", Source = "DNS", ResolvedAtUtc = time1 }
        };
        var cache2 = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.0.5"] = new() { HostName = "SRV-SQL-01", Organization = "Internal SQL", Source = "DNS", ResolvedAtUtc = time2 }
        };

        // Act
        var report = consolidator.Consolidate([cache1, cache2]);

        // Assert
        report.TotalCachesProcessed.Should().Be(2);
        report.Conflicts.Should().BeEmpty();
        report.MergedEntries["10.0.0.5"].Organization.Should().Be("Internal SQL");
        report.MergedEntries["10.0.0.5"].ResolvedAtUtc.Should().Be(time2);
    }

    [Fact]
    public void Consolidate_WhenSameIpHasDifferentHostNames_KeepsMostRecentAndRecordsConflict()
    {
        // Arrange
        var consolidator = new DnsCacheConsolidator();
        var older = DateTimeOffset.UtcNow.AddDays(-1);
        var newer = DateTimeOffset.UtcNow;

        var cacheA = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.1.20"] = new() { HostName = "OLD-SERVER", Source = "DNS", ResolvedAtUtc = older }
        };
        var cacheB = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["10.0.1.20"] = new() { HostName = "NEW-SERVER", Source = "DNS", ResolvedAtUtc = newer }
        };

        // Act
        var report = consolidator.Consolidate([cacheA, cacheB]);

        // Assert
        report.MergedEntries["10.0.1.20"].HostName.Should().Be("NEW-SERVER");
        report.Conflicts.Should().HaveCount(1);
        var conflict = report.Conflicts[0];
        conflict.IpAddress.Should().Be("10.0.1.20");
        conflict.WinningHostName.Should().Be("NEW-SERVER");
        conflict.ConflictingHostName.Should().Be("OLD-SERVER");
    }

    [Fact]
    public void Consolidate_WhenSourceIsDnsVsRdapWithSameTimestamp_PrefersDnsOverRdap()
    {
        // Arrange
        var consolidator = new DnsCacheConsolidator();
        var timestamp = DateTimeOffset.UtcNow;

        var cacheRdap = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["8.8.8.8"] = new() { HostName = "Google LLC", Source = "RDAP", ResolvedAtUtc = timestamp }
        };
        var cacheDns = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["8.8.8.8"] = new() { HostName = "dns.google", Source = "DNS", ResolvedAtUtc = timestamp }
        };

        // Act
        var report = consolidator.Consolidate([cacheRdap, cacheDns]);

        // Assert
        report.MergedEntries["8.8.8.8"].HostName.Should().Be("dns.google");
        report.Conflicts.Should().HaveCount(1);
    }
}
