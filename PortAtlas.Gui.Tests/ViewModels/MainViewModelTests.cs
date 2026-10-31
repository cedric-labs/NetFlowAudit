using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("portatlas-main-vm-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private MainViewModel CreateMainViewModel() =>
        new(
            CommandCatalog.Build(),
            new NoOpRunner(),
            new NullFilePicker(),
            new ImmediateDispatcher(),
            new ProfileStore(new FixedDirectory(_directory)));

    [Fact]
    public void SelectedCommand_WhenChangedAndRestored_KeepsTheTypedValues()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var mapCommand = vm.Commands.Single(c => c.Key == "map");
        var lookupCommand = vm.Commands.Single(c => c.Key == "lookup");

        vm.SelectedCommand = mapCommand;
        vm.CurrentForm.Options.OfType<ChoiceOptionViewModel>().Single(o => o.Name == "--layout").SelectedValue = "grid";

        // Act
        vm.SelectedCommand = lookupCommand;
        vm.SelectedCommand = mapCommand;

        // Assert
        vm.CurrentForm.Options.OfType<ChoiceOptionViewModel>().Single(o => o.Name == "--layout").SelectedValue.Should().Be("grid");
        vm.CurrentForm.Preview.Should().Be("map --layout grid");
    }

    [Fact]
    public void Commands_WhenCatalogIsBuilt_ExcludeAudit()
    {
        // Act
        var vm = CreateMainViewModel();

        // Assert
        vm.Commands.Select(c => c.Key).Should().NotContain("audit");
    }

    private sealed class NoOpRunner : IPortAtlasRunner
    {
        public Task<RunResult> RunAsync(
            IReadOnlyList<string> arguments,
            IProgress<RunOutputLine> output,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RunResult(0));
    }

    private sealed class NullFilePicker : IFilePickerService
    {
        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private sealed class FixedDirectory(string path) : IProfileDirectoryProvider
    {
        public string GetProfileDirectory() => path;
    }
}
