using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class ProfileRoundTripTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("portatlas-roundtrip-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public static TheoryData<string> CommandKeys()
    {
        var data = new TheoryData<string>();
        foreach (var command in CommandCatalog.Build().Commands)
        {
            data.Add(command.Key);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(CommandKeys))]
    public async Task Profile_WhenEveryOptionOfACommandIsFilled_RestoresTheSameCommandLine(string key)
    {
        // Arrange
        var directory = Path.Combine(_root, "profiles");
        var saving = CreateMainViewModel(directory);
        saving.SelectedCommand = saving.Commands.Single(c => c.Key == key);
        FillEveryOption(saving.CurrentForm);
        var expected = saving.CurrentForm.Preview;
        await saving.Profiles.SaveCommand.ExecuteAsync("full");

        // Act
        var loading = CreateMainViewModel(directory);
        await loading.Profiles.LoadCommand.ExecuteAsync("full");
        loading.SelectedCommand = loading.Commands.Single(c => c.Key == key);

        // Assert
        loading.CurrentForm.Preview.Should().Be(expected);
        loading.CurrentForm.Options.Count(o => o is not FlagOptionViewModel).Should().BeGreaterThan(0);
    }

    private static void FillEveryOption(CommandFormViewModel form)
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
                    list.PendingValue = "second";
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
