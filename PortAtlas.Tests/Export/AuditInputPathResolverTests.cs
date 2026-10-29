using AwesomeAssertions;
using PortAtlas.Core.Export;
using Xunit;

namespace PortAtlas.Tests.Export;

public sealed class AuditInputPathResolverTests : IDisposable
{
    private readonly string _directory;

    public AuditInputPathResolverTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "PortAtlas_InputResolver_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        foreach (var name in new[] { "srv-a.csv", "srv-b.csv", "srv-c.ndjson", "notes.txt" })
        {
            File.WriteAllText(Path.Combine(_directory, name), string.Empty);
        }
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
    public void Resolve_WhenDirectoryGiven_ReturnsFilesWithAuditExtensionsOnly()
    {
        // Act
        var files = AuditInputPathResolver.Resolve([_directory], AuditInputPathResolver.AuditLogExtensions);

        // Assert
        files.Select(Path.GetFileName).Should().Equal("srv-a.csv", "srv-b.csv", "srv-c.ndjson");
    }

    [Fact]
    public void Resolve_WhenWildcardPatternGiven_ReturnsMatchingFiles()
    {
        // Act
        var files = AuditInputPathResolver.Resolve([Path.Combine(_directory, "srv-*.csv")], [".csv"]);

        // Assert
        files.Select(Path.GetFileName).Should().Equal("srv-a.csv", "srv-b.csv");
    }

    [Fact]
    public void Resolve_WhenSameFileDesignatedTwice_ReturnsItOnce()
    {
        // Arrange
        var file = Path.Combine(_directory, "srv-a.csv");

        // Act
        var files = AuditInputPathResolver.Resolve([file, Path.Combine(_directory, "srv-a*.csv")], [".csv"]);

        // Assert
        files.Should().ContainSingle().Which.Should().Be(Path.GetFullPath(file));
    }

    [Fact]
    public void Resolve_WhenInputMatchesNothing_ReturnsEmptyList()
    {
        // Act
        var files = AuditInputPathResolver.Resolve([Path.Combine(_directory, "missing.csv"), Path.Combine(_directory, "absent", "*.csv")], [".csv"]);

        // Assert
        files.Should().BeEmpty();
    }
}
