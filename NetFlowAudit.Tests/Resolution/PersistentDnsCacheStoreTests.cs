using FluentAssertions;
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
    public async Task SaveAsync_WhenValidDictionarySupplied_PersistsJsonAndCanBeReloaded()
    {
        // Arrange
        var filePath = Path.Combine(_testDir, "dns_cache.json");
        var store = new PersistentDnsCacheStore(filePath);
        var expected = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["140.82.121.4"] = "lb-140-82-121-4-iad.github.com",
            ["8.8.8.8"] = "dns.google",
            ["192.0.2.1"] = ""
        };

        // Act
        await store.SaveAsync(expected);
        var loaded = store.Load();

        // Assert
        loaded.Should().HaveCount(3);
        loaded["140.82.121.4"].Should().Be("lb-140-82-121-4-iad.github.com");
        loaded["8.8.8.8"].Should().Be("dns.google");
        loaded["192.0.2.1"].Should().BeEmpty();
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
