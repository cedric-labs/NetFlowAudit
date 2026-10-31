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
        UserPreferencesStore preferences)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(profileStore);
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(preferences);

        if (catalog.Commands.Count == 0)
        {
            throw new ArgumentException("The command catalog cannot be empty.", nameof(catalog));
        }

        Commands = catalog.Commands;
        foreach (var command in Commands)
        {
            _formsByKey[command.Key] = new CommandFormViewModel(command, filePicker);
        }

        _selectedCommand = Commands[0];
        _currentForm = _formsByKey[_selectedCommand.Key];
        Run = new RunViewModel(runner, dispatcher, () => CurrentForm.BuiltArguments);
        _preferences = preferences;
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
        return Profiles.InitializeAsync(cancellationToken);
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
        var document = new ProfileDocument();
        foreach (var (key, form) in _formsByKey)
        {
            document.Forms[key] = form.ToState();
        }

        return document;
    }

    private void ApplyProfileDocument(ProfileDocument document)
    {
        foreach (var (key, form) in _formsByKey)
        {
            form.LoadState(document.Forms.TryGetValue(key, out var state) ? state : new CommandFormState());
        }
    }
}
