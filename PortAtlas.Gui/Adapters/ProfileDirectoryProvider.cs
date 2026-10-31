using PortAtlas.Gui.Core.Profiles;

namespace PortAtlas.Gui.Adapters;

/// <summary>
/// Stores profiles in a <c>Data</c> folder next to the executable, and falls back to the per-user folder
/// when the installation folder cannot be written. Profiles found in the per-user folder are copied once.
/// </summary>
/// <param name="applicationDirectory">Folder of the executable.</param>
/// <param name="legacyDataDirectory">Per-user folder used by earlier versions.</param>
public sealed class ProfileDirectoryProvider(string applicationDirectory, string legacyDataDirectory) : IProfileDirectoryProvider
{
    private const string DataFolderName = "Data";
    private const string ProfilesFolderName = "profiles";
    private const string PreferencesFileName = "preferences.json";

    /// <summary>
    /// Per-user folder of the earlier versions, kept as fallback and as migration source.
    /// </summary>
    /// <returns>Absolute path of the per-user data folder.</returns>
    public static string GetLegacyDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PortAtlas");
        }

        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        var configRoot = string.IsNullOrWhiteSpace(xdgConfig)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config")
            : xdgConfig;
        return Path.Combine(configRoot, "portatlas");
    }

    /// <inheritdoc />
    public string GetProfileDirectory()
    {
        var dataDirectory = Path.Combine(applicationDirectory, DataFolderName);
        var profileDirectory = Path.Combine(dataDirectory, ProfilesFolderName);
        var legacyProfiles = Path.Combine(legacyDataDirectory, ProfilesFolderName);

        try
        {
            var firstUse = !Directory.Exists(profileDirectory);
            Directory.CreateDirectory(profileDirectory);
            if (firstUse)
            {
                CopyLegacyData(legacyProfiles, profileDirectory, dataDirectory);
            }

            return profileDirectory;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // An installation under Program Files is read-only for a standard user: the profiles then stay in the
            // per-user folder, as in earlier versions, instead of blocking the application at startup.
            return legacyProfiles;
        }
    }

    private void CopyLegacyData(string legacyProfiles, string profileDirectory, string dataDirectory)
    {
        if (Directory.Exists(legacyProfiles))
        {
            foreach (var file in Directory.EnumerateFiles(legacyProfiles))
            {
                File.Copy(file, Path.Combine(profileDirectory, Path.GetFileName(file)), overwrite: false);
            }
        }

        var legacyPreferences = Path.Combine(legacyDataDirectory, PreferencesFileName);
        if (File.Exists(legacyPreferences))
        {
            File.Copy(legacyPreferences, Path.Combine(dataDirectory, PreferencesFileName), overwrite: false);
        }
    }
}
