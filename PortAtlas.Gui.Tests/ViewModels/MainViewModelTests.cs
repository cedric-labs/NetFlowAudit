using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class MainViewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("portatlas-main-vm-").FullName;
    private string _directory => Path.Combine(_root, "profiles");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private MainViewModel CreateMainViewModel() =>
        new(
            CommandCatalog.Build(),
            new NoOpRunner(),
            new NullFilePicker(),
            new ImmediateDispatcher(),
            new ProfileStore(new FixedDirectory(_directory)),
            new AlwaysConfirm(),
            new UserPreferencesStore(new FixedDirectory(_directory)),
            new ExecutableViewModel(new PortAtlasExecutableLocator(_root, _ => null), new NullFilePicker()));

    [Fact]
    public async Task CycleTheme_WhenInvoked_GoesThroughLightDarkSystemAndPersistsTheChoice()
    {
        // Arrange
        var vm = CreateMainViewModel();

        // Act
        await vm.CycleThemeCommand.ExecuteAsync(null);
        var afterFirst = vm.Theme;
        await vm.CycleThemeCommand.ExecuteAsync(null);
        var afterSecond = vm.Theme;
        await vm.CycleThemeCommand.ExecuteAsync(null);

        // Assert
        afterFirst.Should().Be(ThemeChoice.Light);
        afterSecond.Should().Be(ThemeChoice.Dark);
        vm.Theme.Should().Be(ThemeChoice.System);
        new UserPreferencesStore(new FixedDirectory(_directory)).Load().Theme.Should().Be(ThemeChoice.System);
    }

    [Fact]
    public async Task Profile_WhenSavedWithAnExecutablePath_RestoresThePathOnLoad()
    {
        // Arrange
        var older = Path.Combine(_root, "PortAtlas-3.5.exe");
        await File.WriteAllTextAsync(older, string.Empty);
        var vm = CreateMainViewModel();
        vm.Executable.ConfiguredPath = older;
        await vm.Profiles.SaveCommand.ExecuteAsync("legacy");

        // Act
        vm.Executable.ConfiguredPath = null;
        await vm.Profiles.LoadCommand.ExecuteAsync("legacy");

        // Assert
        vm.Executable.ConfiguredPath.Should().Be(older);
        vm.Executable.ResolvedPath.Should().Be(older);
    }

    [Fact]
    public async Task Profile_WhenSavedWithoutAnExecutablePath_ClearsThePathOnLoad()
    {
        // Arrange
        var vm = CreateMainViewModel();
        await vm.Profiles.SaveCommand.ExecuteAsync("default");
        vm.Executable.ConfiguredPath = Path.Combine(_root, "elsewhere.exe");

        // Act
        await vm.Profiles.LoadCommand.ExecuteAsync("default");

        // Assert
        vm.Executable.ConfiguredPath.Should().BeNull();
    }

    [Fact]
    public void Groups_WhenAFormIsShown_StartOpenExceptAdvanced()
    {
        // Arrange
        var vm = CreateMainViewModel();

        // Act
        vm.SelectedCommand = vm.Commands.Single(c => c.Key == "map");
        var groups = vm.CurrentForm.Groups;

        // Assert
        groups.Where(g => g.Group != OptionGroup.Advanced).Should().OnlyContain(g => g.IsExpanded);
        groups.Single(g => g.Group == OptionGroup.Advanced).IsExpanded.Should().BeFalse();
    }

    [Fact]
    public void GlobalOption_WhenTypedInOneCommand_AppliesToEveryCommand()
    {
        // Arrange
        var vm = CreateMainViewModel();
        var map = vm.Commands.Single(c => c.Key == "map");
        var resolve = vm.Commands.Single(c => c.Key == "resolve");
        vm.SelectedCommand = map;

        // Act
        vm.CurrentForm.Options.OfType<TextOptionViewModel>().Single(o => o.Name == "--config").Value = "homelab-portatlas.json";
        vm.SelectedCommand = resolve;

        // Assert
        vm.CurrentForm.Preview.Should().Contain("--config homelab-portatlas.json");
    }

    [Fact]
    public async Task Profile_WhenOnlyOneCommandStoredTheGlobalOption_SharesItWithTheOthersOnLoad()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var map = new PortAtlas.Gui.Core.Forms.CommandFormState();
        map.SetValue("--config", "homelab-portatlas.json");
        await store.SaveAsync(new ProfileName("older"), new ProfileDocument { Forms = { ["map"] = map } });
        var vm = CreateMainViewModel();

        // Act
        await vm.Profiles.LoadCommand.ExecuteAsync("older");
        vm.SelectedCommand = vm.Commands.Single(c => c.Key == "lookup");

        // Assert
        vm.CurrentForm.Preview.Should().Contain("--config homelab-portatlas.json");
    }

    [Fact]
    public async Task Initialize_WhenAThemeWasStored_RestoresIt()
    {
        // Arrange
        await new UserPreferencesStore(new FixedDirectory(_directory)).UpdateAsync(p => p with { Theme = ThemeChoice.Dark });
        var vm = CreateMainViewModel();

        // Act
        await vm.InitializeAsync();

        // Assert
        vm.Theme.Should().Be(ThemeChoice.Dark);
        vm.ThemeLabel.Should().Be("Theme: Dark");
    }

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
