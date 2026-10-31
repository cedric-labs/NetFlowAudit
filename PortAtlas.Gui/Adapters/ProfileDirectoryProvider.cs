using PortAtlas.Gui.Core.Profiles;

namespace PortAtlas.Gui.Adapters;

/// <summary>
/// Resolves the platform-specific directory where named profiles are stored.
/// </summary>
public sealed class ProfileDirectoryProvider : IProfileDirectoryProvider
{
    /// <inheritdoc />
    public string GetProfileDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appData, "PortAtlas", "profiles");
        }

        var xdgConfig = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (!string.IsNullOrWhiteSpace(xdgConfig))
        {
            return Path.Combine(xdgConfig, "portatlas", "profiles");
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".config", "portatlas", "profiles");
    }
}
