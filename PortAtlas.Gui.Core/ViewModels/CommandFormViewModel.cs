using CommunityToolkit.Mvvm.ComponentModel;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Forms;
using PortAtlas.Gui.Core.Interaction;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// Form view model generated from a <see cref="CommandDescriptor"/>.
/// </summary>
public sealed partial class CommandFormViewModel : ObservableObject
{
    private readonly List<OptionViewModel> _options = [];
    private bool _suppressRefresh;

    /// <summary>
    /// Initializes the command form view model.
    /// </summary>
    /// <param name="command">Command descriptor from the catalog.</param>
    /// <param name="filePicker">File picker service for path options.</param>
    public CommandFormViewModel(CommandDescriptor command, IFilePickerService filePicker)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(filePicker);

        Command = command;
        foreach (var option in command.Options)
        {
            _options.Add(OptionViewModel.Create(option, OptionPresentation.For(option.Name), filePicker, Refresh));
        }

        Groups = [.. Enum.GetValues<OptionGroup>()
            .Select(group => new OptionGroupViewModel(
                group,
                [.. _options.Where(o => o.Presentation.Group == group)]))
            .Where(g => g.Options.Count > 0)];

        Arguments = [.. command.Arguments.Select(arg => new PositionalArgumentViewModel(arg, Refresh))];
        CurrentState = new CommandFormState();
        Refresh();
    }

    /// <summary>
    /// Command descriptor backing this form.
    /// </summary>
    public CommandDescriptor Command { get; }

    /// <summary>
    /// Non-empty option groups ordered by <see cref="OptionGroup"/>.
    /// </summary>
    public IReadOnlyList<OptionGroupViewModel> Groups { get; }

    /// <summary>
    /// All option view models of this form.
    /// </summary>
    public IReadOnlyList<OptionViewModel> Options => _options;

    /// <summary>
    /// Positional arguments of the command.
    /// </summary>
    public IReadOnlyList<PositionalArgumentViewModel> Arguments { get; }

    /// <summary>
    /// Up-to-date form state.
    /// </summary>
    public CommandFormState CurrentState { get; private set; }

    [ObservableProperty]
    private string _preview = string.Empty;

    [ObservableProperty]
    private IReadOnlyList<string> _builtArguments = [];

    /// <summary>
    /// Produces a snapshot of the current <see cref="CommandFormState"/>.
    /// </summary>
    public CommandFormState ToState()
    {
        var state = new CommandFormState();
        foreach (var option in _options)
        {
            option.ApplyTo(state);
        }

        foreach (var argument in Arguments)
        {
            argument.ApplyTo(state);
        }

        return state;
    }

    /// <summary>
    /// Loads <paramref name="state"/> into the form options and arguments.
    /// </summary>
    public void LoadState(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        _suppressRefresh = true;
        try
        {
            foreach (var option in _options)
            {
                option.LoadFrom(state);
            }

            foreach (var argument in Arguments)
            {
                argument.LoadFrom(state);
            }
        }
        finally
        {
            _suppressRefresh = false;
        }

        Refresh();
    }

    private void Refresh()
    {
        if (_suppressRefresh)
        {
            return;
        }

        var state = ToState();
        CurrentState = state;
        var built = ArgumentListBuilder.Build(Command, state);
        BuiltArguments = built.Arguments;
        Preview = built.Preview;
    }
}
