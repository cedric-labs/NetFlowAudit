using FluentAssertions;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Models;
using Xunit;

namespace NetFlowAudit.Tests.Filtering;

public sealed class ProcessExclusionFilterTests
{
    [Fact]
    public void ShouldExclude_WhenProcessIdIsExcluded_ReturnsTrue()
    {
        // Arrange
        var options = new ProcessFilterOptions
        {
            ExcludedProcessIds = [4, 100]
        };
        var filter = new ProcessExclusionFilter(options);
        var process = new ProcessMetadata(4, "System", @"C:\Windows\System32\ntoskrnl.exe", "", "", "SYSTEM");

        // Act
        var result = filter.ShouldExclude(process);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenNameMatchesExactExclusion_ReturnsTrue()
    {
        // Arrange
        var options = new ProcessFilterOptions
        {
            ExcludedExactNames = ["svchost"]
        };
        var filter = new ProcessExclusionFilter(options);
        var process = new ProcessMetadata(1234, "svchost", @"C:\Windows\System32\svchost.exe", "", "", "NETWORK SERVICE");

        // Act
        var result = filter.ShouldExclude(process);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenNameMatchesPrefixExclusion_ReturnsTrue()
    {
        // Arrange
        var options = new ProcessFilterOptions
        {
            ExcludedNamePrefixes = ["vmtools"]
        };
        var filter = new ProcessExclusionFilter(options);
        var process = new ProcessMetadata(5678, "vmtoolsd", @"C:\Program Files\VMware\vmtoolsd.exe", "", "", "SYSTEM");

        // Act
        var result = filter.ShouldExclude(process);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenNameMatchesContainsExclusion_ReturnsTrue()
    {
        // Arrange
        var options = new ProcessFilterOptions
        {
            ExcludedNameContains = ["defender"]
        };
        var filter = new ProcessExclusionFilter(options);
        var process = new ProcessMetadata(9999, "msmpeng_defender_service", @"C:\defender.exe", "", "", "SYSTEM");

        // Act
        var result = filter.ShouldExclude(process);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenProcessDoesNotMatchAnyRule_ReturnsFalse()
    {
        // Arrange
        var options = new ProcessFilterOptions
        {
            ExcludedProcessIds = [4],
            ExcludedExactNames = ["System"],
            ExcludedNameContains = ["defender"],
            ExcludeSystemAccounts = false
        };
        var filter = new ProcessExclusionFilter(options);
        var process = new ProcessMetadata(4321, "w3wp", @"C:\Windows\System32\inetsrv\w3wp.exe", "", "", "IIS APPPOOL");

        // Act
        var result = filter.ShouldExclude(process);

        // Assert
        result.Should().BeFalse();
    }
}
