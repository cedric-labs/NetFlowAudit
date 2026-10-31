using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class ExecutableViewModelTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("portatlas-exe-vm-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Refresh_WhenNoExecutableIsFound_ReportsItAsMissing()
    {
        // Arrange
        var vm = Create(null);

        // Act
        vm.Refresh();

        // Assert
        vm.IsAvailable.Should().BeFalse();
        vm.ResolvedPath.Should().BeNull();
        vm.StatusText.Should().Contain("not found");
    }

    [Fact]
    public void Refresh_WhenTheExecutableIsBesideTheGui_ReportsItsPath()
    {
        // Arrange
        var expected = Path.Combine(_directory, PortAtlasExecutableLocator.ExecutableFileName);
        File.WriteAllText(expected, string.Empty);
        var vm = Create(null);

        // Act
        vm.Refresh();

        // Assert
        vm.IsAvailable.Should().BeTrue();
        vm.ResolvedPath.Should().Be(expected);
    }

    [Fact]
    public async Task Browse_WhenTheUserPicksAnExistingFile_UsesItAndKeepsItAsTheConfiguredPath()
    {
        // Arrange
        var picked = Path.Combine(_directory, "PortAtlas-3.5.exe");
        await File.WriteAllTextAsync(picked, string.Empty);
        var vm = Create(picked);
        vm.Refresh();

        // Act
        await vm.BrowseCommand.ExecuteAsync(null);

        // Assert
        vm.IsAvailable.Should().BeTrue();
        vm.ConfiguredPath.Should().Be(picked);
        vm.ResolvedPath.Should().Be(picked);
    }

    [Fact]
    public async Task Browse_WhenTheUserCancels_KeepsThePreviousState()
    {
        // Arrange
        var vm = Create(null);
        vm.Refresh();

        // Act
        await vm.BrowseCommand.ExecuteAsync(null);

        // Assert
        vm.ConfiguredPath.Should().BeNull();
        vm.IsAvailable.Should().BeFalse();
    }

    [Fact]
    public void ConfiguredPath_WhenCleared_FallsBackToTheAutomaticSearch()
    {
        // Arrange
        var beside = Path.Combine(_directory, PortAtlasExecutableLocator.ExecutableFileName);
        File.WriteAllText(beside, string.Empty);
        var vm = Create(null);
        vm.ConfiguredPath = Path.Combine(_directory, "missing.exe");

        // Act
        vm.ClearCommand.Execute(null);

        // Assert
        vm.ConfiguredPath.Should().BeNull();
        vm.ResolvedPath.Should().Be(beside);
    }

    private ExecutableViewModel Create(string? pickerResult) =>
        new(new PortAtlasExecutableLocator(_directory, _ => null), new StubPicker(pickerResult));

    private sealed class StubPicker(string? result) : IFilePickerService
    {
        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default) => Task.FromResult(result);
    }
}
