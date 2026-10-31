using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Forms;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// Root view model of the PortAtlas GUI application.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly Dictionary<string, CommandFormViewModel> _formsByKey = new(StringComparer.Ordinal);
    private readonly UserPreferencesStore _preferences;
    private bool _isApplyingProfile;

    /// <summary>
    /// Initializes the main view model.
    /// </summary>
    public MainViewModel(
        CommandCatalog catalog,
        IPortAtlasRunner runner,
        IFilePickerService filePicker,
        IUiDispatcher dispatcher,
        ProfileStore profileStore,
        IConfirmationService confirmation,
        UserPreferencesStore preferences,
        ExecutableViewModel executable)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(profileStore);
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(executable);

        if (catalog.Commands.Count == 0)
        {
            throw new ArgumentException("The command catalog cannot be empty.", nameof(catalog));
        }

        Commands = catalog.Commands;
        foreach (var command in Commands)
        {
            _formsByKey[command.Key] = new CommandFormViewModel(command, filePicker);
        }

        foreach (var option in GlobalTextOptions())
        {
            option.PropertyChanged += OnGlobalOptionChanged;
        }

        _selectedCommand = Commands[0];
        _currentForm = _formsByKey[_selectedCommand.Key];
        Run = new RunViewModel(runner, dispatcher, () => CurrentForm.BuiltArguments);
        _preferences = preferences;
        Executable = executable;
        Profiles = new ProfilesViewModel(profileStore, preferences, confirmation, BuildProfileDocument, ApplyProfileDocument);
    }

    /// <summary>
    /// Commands available in the left sidebar.
    /// </summary>
    public IReadOnlyList<CommandDescriptor> Commands { get; }

    [ObservableProperty]
    private CommandDescriptor _selectedCommand;

    [ObservableProperty]
    private CommandFormViewModel _currentForm;

    /// <summary>
    /// View model managing execution and output streaming.
    /// </summary>
    public RunViewModel Run { get; }

    /// <summary>
    /// View model showing and choosing the PortAtlas executable.
    /// </summary>
    public ExecutableViewModel Executable { get; }

    /// <summary>
    /// View model managing profile persistence.
    /// </summary>
    public ProfilesViewModel Profiles { get; }

    /// <summary>
    /// Colour theme currently applied.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeLabel))]
    private ThemeChoice _theme = ThemeChoice.System;

    /// <summary>
    /// Text of the theme button.
    /// </summary>
    public string ThemeLabel => $"Theme: {Theme}";

    /// <summary>
    /// Restores the stored theme and the profile used last.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the restoration.</param>
    /// <returns>A task that ends when the start-up state is restored.</returns>
    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Theme = _preferences.Load().Theme;
        return InitializeProfilesAsync(cancellationToken);
    }

    private async Task InitializeProfilesAsync(CancellationToken cancellationToken)
    {
        await Profiles.InitializeAsync(cancellationToken).ConfigureAwait(true);
        // Checked after the last profile is applied, because the profile may carry the executable path.
        Executable.Refresh();
    }

    [RelayCommand]
    private Task CycleThemeAsync(CancellationToken cancellationToken = default)
    {
        Theme = Theme switch
        {
            ThemeChoice.System => ThemeChoice.Light,
            ThemeChoice.Light => ThemeChoice.Dark,
            _ => ThemeChoice.System
        };

        var chosen = Theme;
        return _preferences.UpdateAsync(p => p with { Theme = chosen }, cancellationToken);
    }

    partial void OnSelectedCommandChanged(CommandDescriptor value)
    {
        if (value is not null && _formsByKey.TryGetValue(value.Key, out var form))
        {
            CurrentForm = form;
        }
    }

    private ProfileDocument BuildProfileDocument()
    {
        var document = new ProfileDocument { ExecutablePath = Executable.ConfiguredPath };
        foreach (var (key, form) in _formsByKey)
        {
            document.Forms[key] = form.ToState();
        }

        return document;
    }

    private void ApplyProfileDocument(ProfileDocument document)
    {
        Executable.ConfiguredPath = document.ExecutablePath;
        // Each form is loaded on its own: propagating while loading would let an empty form wipe the value of another.
        _isApplyingProfile = true;
        try
        {
            foreach (var (key, form) in _formsByKey)
            {
                form.LoadState(document.Forms.TryGetValue(key, out var state) ? state : new CommandFormState());
            }
        }
        finally
        {
            _isApplyingProfile = false;
        }

        ShareGlobalOptions();
    }

    // Options such as --config are declared once on the root command: one value serves every command.
    private IEnumerable<TextOptionViewModel> GlobalTextOptions() =>
        _formsByKey.Values.SelectMany(f => f.Options).OfType<TextOptionViewModel>().Where(o => o.Descriptor.IsGlobal);

    private void OnGlobalOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isApplyingProfile || e.PropertyName != nameof(TextOptionViewModel.Value) || sender is not TextOptionViewModel source)
        {
            return;
        }

        foreach (var other in GlobalTextOptions().Where(o => !ReferenceEquals(o, source) && string.Equals(o.Name, source.Name, StringComparison.Ordinal)))
        {
            other.Value = source.Value;
        }
    }

    private void ShareGlobalOptions()
    {
        foreach (var group in GlobalTextOptions().GroupBy(o => o.Name, StringComparer.Ordinal))
        {
            var shared = group.Select(o => o.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
            foreach (var option in group)
            {
                option.Value = shared;
            }
        }
    }
}
