using AwesomeAssertions;
using PortAtlas.Gui.Core.Execution;

namespace PortAtlas.Gui.Tests.Execution;

public sealed class ExecutableLocatorTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("portatlas-locator-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Locate_WhenNothingIsFound_ThrowsAndNamesBothSearchedLocations()
    {
        // Arrange
        var locator = new PortAtlasExecutableLocator(_directory, _ => null);

        // Act
        var act = locator.Locate;

        // Assert
        act.Should().Throw<FileNotFoundException>()
            .Where(e => e.Message.Contains(_directory, StringComparison.Ordinal)
                && e.Message.Contains(PortAtlasExecutableLocator.EnvironmentVariableName, StringComparison.Ordinal));
    }

    [Fact]
    public void Locate_WhenExecutableIsBesideTheGui_ReturnsItsPath()
    {
        // Arrange
        var expected = Path.Combine(_directory, PortAtlasExecutableLocator.ExecutableFileName);
        File.WriteAllText(expected, string.Empty);
        var locator = new PortAtlasExecutableLocator(_directory, _ => null);

        // Act
        var located = locator.Locate();

        // Assert
        located.Should().Be(expected);
    }

    [Fact]
    public void Locate_WhenOnlyTheEnvironmentVariableIsSet_ReturnsItsPath()
    {
        // Arrange
        var elsewhere = Path.Combine(_directory, "elsewhere.bin");
        File.WriteAllText(elsewhere, string.Empty);
        var locator = new PortAtlasExecutableLocator(Path.Combine(_directory, "empty"), name =>
            name == PortAtlasExecutableLocator.EnvironmentVariableName ? elsewhere : null);

        // Act
        var located = locator.Locate();

        // Assert
        located.Should().Be(elsewhere);
    }
}
