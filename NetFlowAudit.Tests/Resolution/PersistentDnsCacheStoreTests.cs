using AwesomeAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

public sealed class PersistentDnsCacheStoreTests : IAsyncLifetime
{
    private readonly string _testDir = Path.Combine(Path.GetTempPath(), "NetFlowAudit_DnsCache_" + Guid.NewGuid().ToString("N"));

    public Task InitializeAsync()
    {
        Directory.CreateDirectory(_testDir);
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, recursive: true);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsEmptyDictionary()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "non_existent.json");
        var store = new PersistentDnsCacheStore(filePath);

        // Act
        var loaded = store.Load();

        // Assert
        loaded.Should().BeEmpty();
    }

    [Fact]
    public async Task SaveAsync_WhenRichEntriesSupplied_PersistsJsonAndCanBeReloaded()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "dns_cache.json");
        var store = new PersistentDnsCacheStore(filePath);
        var expected = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase)
        {
            ["140.82.121.4"] = new()
            {
                HostName = "lb-140-82-121-4-iad.github.com",
                Source = "DNS"
            },
            ["52.123.242.181"] = new()
            {
                HostName = "Microsoft Corporation",
                Organization = "Microsoft Corporation",
                Country = "US",
                NetworkRange = "52.112.0.0 - 52.127.255.255",
                Source = "RDAP"
            },
            ["192.0.2.1"] = new()
            {
                HostName = "",
                Source = "None"
            }
        };

        // Act
        await store.SaveAsync(expected);
        var loaded = store.Load();

        // Assert
        loaded.Should().HaveCount(3);
        loaded["140.82.121.4"].HostName.Should().Be("lb-140-82-121-4-iad.github.com");
        loaded["140.82.121.4"].Source.Should().Be("DNS");

        loaded["52.123.242.181"].HostName.Should().Be("Microsoft Corporation");
        loaded["52.123.242.181"].Organization.Should().Be("Microsoft Corporation");
        loaded["52.123.242.181"].Country.Should().Be("US");
        loaded["52.123.242.181"].NetworkRange.Should().Be("52.112.0.0 - 52.127.255.255");
        loaded["52.123.242.181"].Source.Should().Be("RDAP");

        loaded["192.0.2.1"].HostName.Should().BeEmpty();
    }

    [Fact]
    public async Task Load_WhenLegacyStringJsonFormatProvided_LoadsEntriesWithBackwardCompatibility()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "legacy_cache.json");
        const string legacyJson = """
        {
          "52.123.242.181": "Microsoft Corporation",
          "8.8.8.8": "dns.google",
          "10.0.0.1": ""
        }
        """;
        await File.WriteAllTextAsync(filePath, legacyJson);
        var store = new PersistentDnsCacheStore(filePath);

        // Act
        var loaded = store.Load();

        // Assert
        loaded.Should().HaveCount(3);
        loaded["52.123.242.181"].HostName.Should().Be("Microsoft Corporation");
        loaded["8.8.8.8"].HostName.Should().Be("dns.google");
        loaded["10.0.0.1"].HostName.Should().BeEmpty();
    }

    [Fact]
    public async Task Load_WhenJsonIsCorrupted_ReturnsEmptyDictionaryWithoutThrowing()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "corrupted.json");
        await File.WriteAllTextAsync(filePath, "{ invalid_json_syntax: true }");
        var store = new PersistentDnsCacheStore(filePath);

        // Act
        var loaded = store.Load();

        // Assert
        loaded.Should().BeEmpty();
    }
}
