using CommunityToolkit.Mvvm.ComponentModel;
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

    /// <summary>
    /// Initializes the main view model.
    /// </summary>
    public MainViewModel(
        CommandCatalog catalog,
        IPortAtlasRunner runner,
        IFilePickerService filePicker,
        IUiDispatcher dispatcher,
        ProfileStore profileStore)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(profileStore);

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
        Profiles = new ProfilesViewModel(profileStore, BuildProfileDocument, ApplyProfileDocument);
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
