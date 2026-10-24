using System.Text.Json;
using FluentAssertions;
using NetFlowAudit.Cli.Configuration;
using Xunit;

namespace NetFlowAudit.Tests.Configuration;

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
        parsed.CustomHostAliases.Should().ContainKey("10.0.0.5");
        parsed.ExcludedPids.Should().Contain([0, 4]);
    }

    [Fact]
    public void GenerateDefaultConfigFile_WhenTargetIsDirectory_GeneratesFileInsideDirectory()
    {
        // Arrange & Act
        var resultPath = ConfigurationTemplateGenerator.GenerateDefaultConfigFile(_tempDirectory);

        // Assert
        File.Exists(resultPath).Should().BeTrue();
        Path.GetFileName(resultPath).Should().Be("netflowaudit.json");
    }
}
