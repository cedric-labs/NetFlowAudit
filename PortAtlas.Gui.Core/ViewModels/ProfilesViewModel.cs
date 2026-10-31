using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// View model that saves and loads named profiles.
/// </summary>
public sealed partial class ProfilesViewModel : ObservableObject
{
    private readonly ProfileStore _store;
    private readonly UserPreferencesStore _preferences;
    private readonly IConfirmationService _confirmation;
    private readonly Func<ProfileDocument> _readDocument;
    private readonly Action<ProfileDocument> _applyDocument;

    /// <summary>
    /// Initializes the profiles view model and loads the existing profile names.
    /// </summary>
    /// <param name="store">Profile persistence store.</param>
    /// <param name="preferences">Store that remembers the last profile used.</param>
    /// <param name="confirmation">Asks the user before a stored profile is overwritten.</param>
    /// <param name="readDocument">Builds a <see cref="ProfileDocument"/> from the current forms.</param>
    /// <param name="applyDocument">Applies a loaded <see cref="ProfileDocument"/> to the forms.</param>
    public ProfilesViewModel(
        ProfileStore store,
        UserPreferencesStore preferences,
        IConfirmationService confirmation,
        Func<ProfileDocument> readDocument,
        Action<ProfileDocument> applyDocument)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(preferences);
        ArgumentNullException.ThrowIfNull(confirmation);
        ArgumentNullException.ThrowIfNull(readDocument);
        ArgumentNullException.ThrowIfNull(applyDocument);

        _store = store;
        _preferences = preferences;
        _confirmation = confirmation;
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
        // Without a typed name, the profile that is currently selected (loaded) is the one to update.
        var candidate = name ?? (string.IsNullOrWhiteSpace(ProfileNameInput) ? SelectedProfile : ProfileNameInput);
        if (string.IsNullOrWhiteSpace(candidate))
        {
            ErrorMessage = "Type a profile name, or select a profile to update.";
            return;
        }

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

        var exists = _store.ListNames().Contains(profileName.Value, StringComparer.OrdinalIgnoreCase);
        if (exists && !await _confirmation.ConfirmAsync($"Overwrite the profile \"{profileName.Value}\"?", cancellationToken).ConfigureAwait(true))
        {
            return;
        }

        await _store.SaveAsync(profileName, _readDocument(), cancellationToken).ConfigureAwait(true);
        RefreshNames();
        SelectedProfile = profileName.Value;
        ProfileNameInput = string.Empty;
        await RememberLastProfileAsync(profileName, cancellationToken).ConfigureAwait(true);
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
            await RememberLastProfileAsync(profileName, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is ProfileFileException or FileNotFoundException)
        {
            ErrorMessage = ex.Message;
        }
    }

    /// <summary>
    /// Loads the profile used last, when it still exists.
    /// </summary>
    /// <param name="cancellationToken">Token that cancels the load.</param>
    /// <returns>A task that ends when the profile is applied or skipped.</returns>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var last = _preferences.Load().LastProfile;
        if (string.IsNullOrWhiteSpace(last) || !Names.Contains(last, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        await LoadAsync(last, cancellationToken).ConfigureAwait(true);
    }

    private async Task RememberLastProfileAsync(ProfileName name, CancellationToken cancellationToken)
    {
        try
        {
            await _preferences.UpdateAsync(p => p with { LastProfile = name.Value }, cancellationToken).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The profile itself is saved or loaded; only the "reopen it next time" convenience is lost, and the user is told why.
            ErrorMessage = $"The last profile could not be remembered: {ex.Message}";
        }
    }
}
