using System.Text.Json;
using AwesomeAssertions;
using PortAtlas.Cli.Configuration;
using PortAtlas.Core.Export;
using Xunit;

namespace PortAtlas.Tests.Configuration;

public sealed class ConfigurationTemplateGeneratorTests : IDisposable
{
    private readonly string _tempDirectory;

    public ConfigurationTemplateGeneratorTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "netflow_config_tests_" + Guid.NewGuid().ToString("N"));
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
    public void GenerateDefaultConfigFile_WhenTargetIsSpecifiedPath_CreatesValidJsonFileWithAllOptions()
    {
        // Arrange
        var targetFile = Path.Combine(_tempDirectory, "custom_audit_config.json");

        // Act
        var resultPath = ConfigurationTemplateGenerator.GenerateDefaultConfigFile(targetFile);

        // Assert
        File.Exists(resultPath).Should().BeTrue();
        resultPath.Should().Be(Path.GetFullPath(targetFile));

        var json = File.ReadAllText(resultPath);
        json.Should().Contain("\"ScanIntervalSeconds\": 5");
        json.Should().Contain("\"OutputCsvPath\": \"netflow_audit.csv\"");
        json.Should().Contain("\"ReloadHistoryLimit\"");
        json.Should().Contain("\"CustomHostAliases\"");

        var parsed = JsonSerializer.Deserialize<AppConfiguration>(json);
        parsed.Should().NotBeNull();
        parsed!.ScanIntervalSeconds.Should().Be(5);
        parsed.OutputCsvPath.Should().Be("netflow_audit.csv");
        parsed.ReloadHistoryLimit.Should().BeNull();
        parsed.ExcludedPids.Should().Contain([0, 4]);
    }

    [Fact]
    public void GenerateDefaultConfigFile_WhenGenerated_WritesTheBuiltInDefaultsWithEveryColumnListed()
    {
        // Arrange
        var targetFile = Path.Combine(_tempDirectory, "defaults.json");
        var columnNames = AuditCsvColumnCatalog.GetAllDescriptors().Select(d => d.Name).ToList();

        // Act
        ConfigurationTemplateGenerator.GenerateDefaultConfigFile(targetFile);

        // Assert
        var parsed = JsonSerializer.Deserialize<AppConfiguration>(File.ReadAllText(targetFile));
        parsed.Should().BeEquivalentTo(new AppConfiguration { CsvColumns = columnNames });
    }

    [Fact]
    public void GenerateDefaultConfigFile_WhenTargetIsDirectory_GeneratesFileInsideDirectory()
    {
        // Arrange & Act
        var resultPath = ConfigurationTemplateGenerator.GenerateDefaultConfigFile(_tempDirectory);

        // Assert
        File.Exists(resultPath).Should().BeTrue();
        Path.GetFileName(resultPath).Should().Be("portatlas.json");
    }
}
