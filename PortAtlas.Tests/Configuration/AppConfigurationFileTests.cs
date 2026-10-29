using AwesomeAssertions;
using PortAtlas.Cli.Configuration;
using Xunit;

namespace PortAtlas.Tests.Configuration;

public sealed class AppConfigurationFileTests : IDisposable
{
    private readonly string _directory;

    public AppConfigurationFileTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PortAtlas_ConfigFile_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
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
    public void Load_WhenOutputPathsContainTokens_ExpandsThem()
    {
        // Arrange
        var path = WriteConfiguration("""{ "OutputCsvPath": "audit_{HostName}.csv", "DnsCacheFilePath": "cache_{HostName}.json" }""");

        // Act
        var loaded = AppConfigurationFile.Load(path);

        // Assert
        loaded.Configuration.OutputCsvPath.Should().NotContain("{HostName}").And.StartWith("audit_");
        loaded.Configuration.DnsCacheFilePath.Should().NotContain("{HostName}").And.StartWith("cache_");
    }

    [Fact]
    public void Load_WhenFileIsMissing_KeepsSystemAccountProcessesRecorded()
    {
        // Act
        var loaded = AppConfigurationFile.Load(Path.Combine(_directory, "absent.json"));

        // Assert
        loaded.Configuration.ExcludeSystemAccounts.Should().BeFalse();
    }

    [Fact]
    public void Load_WhenFileIsMissing_WaitsThreeSecondsForReverseDns()
    {
        // Act
        var loaded = AppConfigurationFile.Load(Path.Combine(_directory, "absent.json"));

        // Assert
        loaded.Configuration.DnsResolutionTimeoutMs.Should().Be(3000);
    }

    [Fact]
    public void Load_WhenRemovedIsolatedModeKeyIsTrue_ThrowsConfigurationFileException()
    {
        // Arrange
        var path = WriteConfiguration("""{ "IsolatedMode": true }""");

        // Act
        var act = () => AppConfigurationFile.Load(path);

        // Assert
        act.Should().Throw<ConfigurationFileException>().WithMessage("*IsolatedMode*ResolveIpOwners*");
    }

    [Fact]
    public void Load_WhenRemovedIsolatedModeKeyIsFalse_LoadsTheOtherValues()
    {
        // Arrange
        var path = WriteConfiguration("""{ "IsolatedMode": false, "ScanIntervalSeconds": 9 }""");

        // Act
        var loaded = AppConfigurationFile.Load(path);

        // Assert
        loaded.Configuration.ScanIntervalSeconds.Should().Be(9);
    }

    [Fact]
    public void Load_WhenOnlyLegacyNetflowauditJsonExists_IgnoresIt()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_directory, "netflowaudit.json"), """{ "ScanIntervalSeconds": 42 }""");

        // Act
        var loaded = AppConfigurationFile.Load(Path.Combine(_directory, AppConfigurationFile.DefaultPath));

        // Assert
        loaded.Configuration.ScanIntervalSeconds.Should().Be(5);
    }

    private string WriteConfiguration(string json)
    {
        var path = Path.Combine(_directory, "portatlas.json");
        File.WriteAllText(path, json);
        return path;
    }
}
