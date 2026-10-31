using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;
using PortAtlas.Gui.Tests.Views;
using PortAtlas.Gui.Views;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace PortAtlas.Gui.Tests.Views;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

public sealed class MainWindowTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("portatlas-window-").FullName;
    private string _directory => Path.Combine(_root, "profiles");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [AvaloniaFact]
    public void MainWindow_WhenShown_ListsEveryCatalogCommand()
    {
        // Arrange
        var catalog = CommandCatalog.Build();
        var vm = new MainViewModel(
            catalog,
            new NoOpRunner(),
            new NullFilePicker(),
            new ImmediateDispatcher(),
            new ProfileStore(new FixedDirectory(_directory)),
            new AlwaysConfirm(),
            new UserPreferencesStore(new FixedDirectory(_directory)),
            new ExecutableViewModel(new PortAtlasExecutableLocator(_root, _ => null), new NullFilePicker()));

        var window = new MainWindow { DataContext = vm };

        // Act
        window.Show();
        var listBox = window.FindControl<ListBox>("CommandsList");
        var listedKeys = listBox?.Items.OfType<CommandDescriptor>().Select(c => c.Key).ToArray() ?? [];
        window.Close();

        // Assert
        listedKeys.Should().Equal(catalog.Commands.Select(c => c.Key));
    }

    [AvaloniaFact]
    public async Task MainWindow_WhenConfigInitIsRunEndToEnd_CreatesFileAndCompletesWithZeroExitCode()
    {
        // Arrange
        var configPath = Path.Combine(_directory, "homelab-portatlas.json");
        var locator = new PortAtlasExecutableLocator(AppContext.BaseDirectory, Environment.GetEnvironmentVariable);
        var runner = new PortAtlasProcessRunner(locator);
        var vm = new MainViewModel(
            CommandCatalog.Build(),
            runner,
            new NullFilePicker(),
            new ImmediateDispatcher(),
            new ProfileStore(new FixedDirectory(_directory)),
            new AlwaysConfirm(),
            new UserPreferencesStore(new FixedDirectory(_directory)),
            new ExecutableViewModel(locator, new NullFilePicker()));

        var window = new MainWindow { DataContext = vm };

        // Act
        window.Show();
        vm.SelectedCommand = vm.Commands.Single(c => c.Key == "config init");
        vm.CurrentForm.Arguments.Single(a => a.Name == "path").Value = configPath;
        await vm.Run.RunCommand.ExecuteAsync(null);
        window.Close();

        // Assert
        vm.Run.State.Should().Be(RunState.Completed);
        vm.Run.ExitCode.Should().Be(0);
        File.Exists(configPath).Should().BeTrue();
    }

    private sealed class AlwaysConfirm : IConfirmationService
    {
        public Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken = default) => Task.FromResult(true);
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
