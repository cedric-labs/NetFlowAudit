namespace PortAtlas.Gui.Core.Profiles;

/// <summary>
/// Supplies the directory profiles are stored in, so that the core never reads system folders itself.
/// </summary>
public interface IProfileDirectoryProvider
{
    /// <summary>
    /// Returns the directory of the profiles.
    /// </summary>
    /// <returns>Absolute path of the directory; it need not exist yet.</returns>
    string GetProfileDirectory();
}
