using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortAtlas.Gui.Core.Profiles;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// View model that saves and loads named profiles.
/// </summary>
public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileStore _store;
    private readonly Func<ProfileDocument> _readDocument;
    private readonly Action<ProfileDocument> _applyDocument;

    /// <summary>
    /// Initializes the profiles view model and loads the existing profile names.
    /// </summary>
    /// <param name="store">Profile persistence store.</param>
    /// <param name="readDocument">Builds a <see cref="ProfileDocument"/> from the current forms.</param>
    /// <param name="applyDocument">Applies a loaded <see cref="ProfileDocument"/> to the forms.</param>
    public ProfilesViewModel(
        ProfileStore store,
        Func<ProfileDocument> readDocument,
        Action<ProfileDocument> applyDocument)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(readDocument);
        ArgumentNullException.ThrowIfNull(applyDocument);

        _store = store;
        _readDocument = readDocument;
        _applyDocument = applyDocument;
        RefreshNames();
    }

    [ObservableProperty]
    private string _profileNameInput = string.Empty;

    [ObservableProperty]
    private string? _selectedProfile;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Names of the stored profiles.
    /// </summary>
    public ObservableCollection<string> Names { get; } = [];

    /// <summary>
    /// Reloads the list of stored profile names.
    /// </summary>
    public void RefreshNames()
    {
        Names.Clear();
        foreach (var name in _store.ListNames())
        {
            Names.Add(name);
        }
    }

    [RelayCommand]
    private async Task SaveAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        var candidate = name ?? ProfileNameInput;
        ProfileName profileName;
        try
        {
            profileName = new ProfileName(candidate);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        await _store.SaveAsync(profileName, _readDocument(), cancellationToken).ConfigureAwait(true);
        RefreshNames();
        SelectedProfile = profileName.Value;
    }

    [RelayCommand]
    private async Task LoadAsync(string? name = null, CancellationToken cancellationToken = default)
    {
        ErrorMessage = null;
        var candidate = name ?? SelectedProfile ?? ProfileNameInput;
        ProfileName profileName;
        try
        {
            profileName = new ProfileName(candidate ?? string.Empty);
        }
        catch (ArgumentException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        try
        {
            var document = await _store.LoadAsync(profileName, cancellationToken).ConfigureAwait(true);
            _applyDocument(document);
            SelectedProfile = profileName.Value;
        }
        catch (Exception ex) when (ex is ProfileFileException or FileNotFoundException)
        {
            ErrorMessage = ex.Message;
        }
    }
}
