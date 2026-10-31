using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// Shows which PortAtlas executable the GUI runs and lets the user choose another one at any time.
/// </summary>
/// <param name="locator">Finds the executable and holds the path the user chose.</param>
/// <param name="filePicker">Opens the dialog that picks the executable.</param>
public sealed partial class ExecutableViewModel(IPortAtlasExecutableLocator locator, IFilePickerService filePicker) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(IsAvailable))]
    private string? _resolvedPath;

    /// <summary>
    /// Path the user chose explicitly, or <see langword="null"/> when the automatic search applies. Saved in profiles.
    /// </summary>
    public string? ConfiguredPath
    {
        get => locator.ConfiguredPath;
        set
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? null : value;
            if (string.Equals(locator.ConfiguredPath, normalized, StringComparison.Ordinal))
            {
                return;
            }

            locator.ConfiguredPath = normalized;
            OnPropertyChanged();
            Refresh();
        }
    }

    /// <summary>
    /// Whether an executable was found.
    /// </summary>
    public bool IsAvailable => ResolvedPath is not null;

    /// <summary>
    /// One-line description of the executable in use, or of why none is available.
    /// </summary>
    public string StatusText => ResolvedPath is null
        ? $"{PortAtlasExecutableLocator.ExecutableFileName} was not found next to the GUI or at the configured path."
        : ResolvedPath;

    /// <summary>
    /// Looks for the executable again and updates <see cref="ResolvedPath"/>.
    /// </summary>
    public void Refresh() => ResolvedPath = locator.TryLocate(out var path) ? path : null;

    [RelayCommand]
    private async Task BrowseAsync(CancellationToken cancellationToken = default)
    {
        var picked = await filePicker.PickAsync(PathKind.FileToRead, cancellationToken).ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(picked))
        {
            ConfiguredPath = picked;
        }
    }

    [RelayCommand]
    private void Clear() => ConfiguredPath = null;
}
