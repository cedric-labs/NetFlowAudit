using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Forms;
using PortAtlas.Gui.Core.Interaction;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// View model of a single CLI option in a command form.
/// </summary>
public abstract partial class OptionViewModel : ObservableObject
{
    private readonly Action _onChanged;

    /// <summary>
    /// Initializes the option view model.
    /// </summary>
    /// <param name="descriptor">Catalog descriptor of the option.</param>
    /// <param name="presentation">Presentation metadata of the option.</param>
    /// <param name="onChanged">Callback invoked whenever the option value changes.</param>
    protected OptionViewModel(OptionDescriptor descriptor, OptionPresentationEntry presentation, Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(onChanged);

        Descriptor = descriptor;
        Presentation = presentation;
        _onChanged = onChanged;
    }

    /// <summary>
    /// Catalog descriptor of the option.
    /// </summary>
    public OptionDescriptor Descriptor { get; }

    /// <summary>
    /// Presentation metadata (group and path kind).
    /// </summary>
    public OptionPresentationEntry Presentation { get; }

    /// <summary>
    /// Long name of the option (e.g. <c>--layout</c>).
    /// </summary>
    public string Name => Descriptor.Name;

    /// <summary>
    /// Help text of the option.
    /// </summary>
    public string Description => Descriptor.Description;

    /// <summary>
    /// Whether the CLI requires this option.
    /// </summary>
    public bool IsRequired => Descriptor.IsRequired;

    /// <summary>
    /// Whether this option represents a file or directory path.
    /// </summary>
    public bool HasPathPicker => Presentation.PathKind != PathKind.None;

    /// <summary>
    /// Whether the user may also pick a directory, in addition to a file.
    /// </summary>
    public bool HasDirectoryPicker => Presentation.PathKind == PathKind.FileOrDirectoryToRead;

    /// <summary>
    /// Kind of path the file picker is asked for: a file when the option accepts a file or a directory.
    /// </summary>
    protected PathKind FilePickerKind =>
        Presentation.PathKind == PathKind.FileOrDirectoryToRead ? PathKind.FileToRead : Presentation.PathKind;

    /// <summary>
    /// Creates the specialized <see cref="OptionViewModel"/> for <paramref name="descriptor"/>.
    /// </summary>
    public static OptionViewModel Create(
        OptionDescriptor descriptor,
        OptionPresentationEntry presentation,
        IFilePickerService filePicker,
        Action onChanged)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(filePicker);
        ArgumentNullException.ThrowIfNull(onChanged);

        return descriptor.Kind switch
        {
            OptionValueKind.Flag => new FlagOptionViewModel(descriptor, presentation, onChanged),
            OptionValueKind.Text => new TextOptionViewModel(descriptor, presentation, filePicker, onChanged),
            OptionValueKind.Integer => new IntegerOptionViewModel(descriptor, presentation, onChanged),
            OptionValueKind.Choice => new ChoiceOptionViewModel(descriptor, presentation, onChanged),
            OptionValueKind.TextList => new TextListOptionViewModel(descriptor, presentation, filePicker, onChanged),
            _ => throw new ArgumentOutOfRangeException(nameof(descriptor), descriptor.Kind, "Unsupported option kind.")
        };
    }

    /// <summary>
    /// Writes the current value of this option into <paramref name="state"/>.
    /// </summary>
    public abstract void ApplyTo(CommandFormState state);

    /// <summary>
    /// Restores the value of this option from <paramref name="state"/>.
    /// </summary>
    public abstract void LoadFrom(CommandFormState state);

    /// <summary>
    /// Notifies the parent form that the option value changed.
    /// </summary>
    protected void NotifyChanged() => _onChanged();
}

/// <summary>
/// Boolean flag option (<see cref="OptionValueKind.Flag"/>).
/// </summary>
public sealed partial class FlagOptionViewModel(
    OptionDescriptor descriptor,
    OptionPresentationEntry presentation,
    Action onChanged) : OptionViewModel(descriptor, presentation, onChanged)
{
    [ObservableProperty]
    private bool _isChecked;

    partial void OnIsCheckedChanged(bool value) => NotifyChanged();

    /// <inheritdoc />
    public override void ApplyTo(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.SetFlag(Name, IsChecked);
    }

    /// <inheritdoc />
    public override void LoadFrom(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        IsChecked = state.Flags.TryGetValue(Name, out var isOn) && isOn;
    }
}

/// <summary>
/// Single free-text or path option (<see cref="OptionValueKind.Text"/>).
/// </summary>
public sealed partial class TextOptionViewModel(
    OptionDescriptor descriptor,
    OptionPresentationEntry presentation,
    IFilePickerService filePicker,
    Action onChanged) : OptionViewModel(descriptor, presentation, onChanged)
{
    [ObservableProperty]
    private string _value = string.Empty;

    partial void OnValueChanged(string value) => NotifyChanged();

    [RelayCommand]
    private async Task BrowseAsync(CancellationToken cancellationToken = default)
    {
        if (Presentation.PathKind == PathKind.None)
        {
            return;
        }

        var picked = await filePicker.PickAsync(FilePickerKind, cancellationToken).ConfigureAwait(false);
        if (picked is not null)
        {
            Value = picked;
        }
    }

    /// <inheritdoc />
    public override void ApplyTo(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.SetValue(Name, Value);
    }

    /// <inheritdoc />
    public override void LoadFrom(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Value = state.Values.TryGetValue(Name, out var stored) ? stored : string.Empty;
    }
}

/// <summary>
/// Integer option (<see cref="OptionValueKind.Integer"/>); kept as raw text so the CLI remains the sole validation authority.
/// </summary>
public sealed partial class IntegerOptionViewModel(
    OptionDescriptor descriptor,
    OptionPresentationEntry presentation,
    Action onChanged) : OptionViewModel(descriptor, presentation, onChanged)
{
    [ObservableProperty]
    private string _value = string.Empty;

    partial void OnValueChanged(string value) => NotifyChanged();

    /// <inheritdoc />
    public override void ApplyTo(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.SetValue(Name, Value);
    }

    /// <inheritdoc />
    public override void LoadFrom(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Value = state.Values.TryGetValue(Name, out var stored) ? stored : string.Empty;
    }
}

/// <summary>
/// Closed-set choice option (<see cref="OptionValueKind.Choice"/>); starts empty so the option is omitted unless chosen.
/// </summary>
public sealed partial class ChoiceOptionViewModel(
    OptionDescriptor descriptor,
    OptionPresentationEntry presentation,
    Action onChanged) : OptionViewModel(descriptor, presentation, onChanged)
{
    [ObservableProperty]
    private string? _selectedValue;

    /// <summary>
    /// Accepted values exposed by the CLI completion sources.
    /// </summary>
    public IReadOnlyList<string> AcceptedValues => Descriptor.AcceptedValues;

    /// <summary>
    /// Accepted values with their explanation, for the drop-down list.
    /// </summary>
    public IReadOnlyList<ChoiceItem> Choices => _choices ??= [.. AcceptedValues.Select(value => new ChoiceItem(value, ChoiceValueDescriptions.For(Name, value)))];

    /// <summary>
    /// Selected entry of <see cref="Choices"/>, or <see langword="null"/> when the option is omitted.
    /// </summary>
    public ChoiceItem? SelectedChoice
    {
        get => Choices.FirstOrDefault(choice => string.Equals(choice.Value, SelectedValue, StringComparison.Ordinal));
        set => SelectedValue = value?.Value;
    }

    /// <summary>
    /// Explanation of the selected value, empty when nothing is selected.
    /// </summary>
    public string SelectedValueDescription => SelectedChoice?.Description ?? string.Empty;

    private IReadOnlyList<ChoiceItem>? _choices;

    partial void OnSelectedValueChanged(string? value)
    {
        OnPropertyChanged(nameof(SelectedChoice));
        OnPropertyChanged(nameof(SelectedValueDescription));
        NotifyChanged();
    }

    [RelayCommand]
    private void Clear() => SelectedValue = null;

    /// <inheritdoc />
    public override void ApplyTo(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.SetValue(Name, SelectedValue);
    }

    /// <inheritdoc />
    public override void LoadFrom(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        SelectedValue = state.Values.TryGetValue(Name, out var stored) ? stored : null;
    }
}

/// <summary>
/// Repeatable text list option (<see cref="OptionValueKind.TextList"/>).
/// </summary>
public sealed partial class TextListOptionViewModel(
    OptionDescriptor descriptor,
    OptionPresentationEntry presentation,
    IFilePickerService filePicker,
    Action onChanged) : OptionViewModel(descriptor, presentation, onChanged)
{
    [ObservableProperty]
    private string _pendingValue = string.Empty;

    // The typed text counts as soon as it is typed: forgetting to press Add must not drop it from the profile or the command line.
    partial void OnPendingValueChanged(string value) => NotifyChanged();

    /// <summary>
    /// Values currently added to the repeatable option.
    /// </summary>
    public ObservableCollection<string> Values { get; } = [];

    [RelayCommand]
    private void AddItem(string? value = null)
    {
        var candidate = value ?? PendingValue;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return;
        }

        Values.Add(candidate);
        if (value is null)
        {
            PendingValue = string.Empty;
        }

        NotifyChanged();
    }

    [RelayCommand]
    private void RemoveItem(string? item)
    {
        if (item is not null && Values.Remove(item))
        {
            NotifyChanged();
        }
    }

    [RelayCommand]
    private async Task BrowseAsync(CancellationToken cancellationToken = default)
    {
        if (Presentation.PathKind == PathKind.None)
        {
            return;
        }

        var picked = await filePicker.PickAsync(FilePickerKind, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            Values.Add(picked);
            NotifyChanged();
        }
    }

    [RelayCommand]
    private async Task BrowseDirectoryAsync(CancellationToken cancellationToken = default)
    {
        if (!HasDirectoryPicker)
        {
            return;
        }

        var picked = await filePicker.PickAsync(PathKind.Directory, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            Values.Add(picked);
            NotifyChanged();
        }
    }

    /// <inheritdoc />
    public override void ApplyTo(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.SetList(Name, string.IsNullOrWhiteSpace(PendingValue) ? Values : [.. Values, PendingValue]);
    }

    /// <inheritdoc />
    public override void LoadFrom(CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Values.Clear();
        PendingValue = string.Empty;
        if (state.Lists.TryGetValue(Name, out var stored))
        {
            foreach (var item in stored)
            {
                Values.Add(item);
            }
        }

        NotifyChanged();
    }
}
