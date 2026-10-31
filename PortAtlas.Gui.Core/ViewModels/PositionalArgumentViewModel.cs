using CommunityToolkit.Mvvm.ComponentModel;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Forms;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// View model of a positional argument in a command form.
/// </summary>
public sealed partial class PositionalArgumentViewModel : ObservableObject
{
    private readonly Action _onChanged;

    /// <summary>
    /// Initializes the positional argument view model.
    /// </summary>
    /// <param name="descriptor">Argument descriptor from the catalog.</param>
    /// <param name="onChanged">Callback invoked when the value changes.</param>
    public PositionalArgumentViewModel(ArgumentDescriptor descriptor, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(onChanged);

        Descriptor = descriptor;
        _onChanged = onChanged;
    }

    /// <summary>
    /// Argument descriptor.
    /// </summary>
    public ArgumentDescriptor Descriptor { get; }

    /// <summary>
    /// Name of the positional argument.
    /// </summary>
    public string Name => Descriptor.Name;

    /// <summary>
    /// Help text of the positional argument.
    /// </summary>
    public string Description => Descriptor.Description;

    /// <summary>
    /// Whether the positional argument is required by the CLI.
    /// </summary>
    public bool IsRequired => Descriptor.IsRequired;

    [ObservableProperty]
    private string _value = string.Empty;

    partial void OnValueChanged(string value) => _onChanged();

    /// <summary>
    /// Writes the current argument value into <paramref name="state"/>.
    /// </summary>
    public void ApplyTo(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.SetArgument(Name, Value);
    }

    /// <summary>
    /// Restores the argument value from <paramref name="state"/>.
    /// </summary>
    public void LoadFrom(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Value = state.Arguments.TryGetValue(Name, out var stored) ? stored : string.Empty;
    }
}
