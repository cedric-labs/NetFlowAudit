using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;
using PortAtlas.Gui.Views;

namespace PortAtlas.Gui.Tests.Views;

public sealed class WindowRoundTripTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("portatlas-window-rt-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [AvaloniaFact]
    public async Task Window_WhenEveryCommandIsFilledThenProfileSavedAndLoaded_KeepsEveryCommandLine()
    {
        // Arrange
        var directory = Path.Combine(_root, "profiles");
        var vm = CreateMainViewModel(directory);
        var window = new MainWindow { DataContext = vm };
        window.Show();
        var expected = new Dictionary<string, string>();
        foreach (var command in vm.Commands)
        {
            vm.SelectedCommand = command;
            Dispatcher.UIThread.RunJobs();
            Fill(vm.CurrentForm);
            Dispatcher.UIThread.RunJobs();
            expected[command.Key] = vm.CurrentForm.Preview;
        }

        // Act
        await vm.Profiles.SaveCommand.ExecuteAsync("full");
        foreach (var command in vm.Commands)
        {
            vm.SelectedCommand = command;
            Dispatcher.UIThread.RunJobs();
        }

        await vm.Profiles.LoadCommand.ExecuteAsync("full");
        var actual = new Dictionary<string, string>();
        foreach (var command in vm.Commands)
        {
            vm.SelectedCommand = command;
            Dispatcher.UIThread.RunJobs();
            actual[command.Key] = vm.CurrentForm.Preview;
        }

        window.Close();

        // Assert
        actual.Should().BeEquivalentTo(expected);
    }

    private static void Fill(CommandFormViewModel form)
    {
        foreach (var option in form.Options)
        {
            switch (option)
            {
                case FlagOptionViewModel flag:
                    flag.IsChecked = true;
                    break;
                case TextOptionViewModel text:
                    text.Value = $"text-{text.Name.TrimStart('-')}";
                    break;
                case IntegerOptionViewModel integer:
                    integer.Value = "7";
                    break;
                case ChoiceOptionViewModel choice:
                    choice.SelectedValue = choice.AcceptedValues[^1];
                    break;
                case TextListOptionViewModel list:
                    list.Values.Add("first");
                    break;
                default:
                    break;
            }
        }

        foreach (var argument in form.Arguments)
        {
            argument.Value = $"arg-{argument.Name}";
        }
    }

    private MainViewModel CreateMainViewModel(string directory) =>
        new(
            CommandCatalog.Build(),
            new NoOpRunner(),
            new NullFilePicker(),
            new ImmediateDispatcher(),
            new ProfileStore(new FixedDirectory(directory)),
            new AlwaysConfirm(),
            new UserPreferencesStore(new FixedDirectory(directory)),
            new ExecutableViewModel(new PortAtlasExecutableLocator(_root, _ => null), new NullFilePicker()));

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
