namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Finds the PortAtlas executable a command is run with.
/// </summary>
public interface IPortAtlasExecutableLocator
{
    /// <summary>
    /// Path the user chose explicitly, tried before the automatic search; <see langword="null"/> means automatic search only.
    /// </summary>
    string? ConfiguredPath { get; set; }

    /// <summary>
    /// Looks for the executable without throwing.
    /// </summary>
    /// <param name="path">Full path of the executable when found; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when an existing executable was found.</returns>
    bool TryLocate(out string? path);

    /// <summary>
    /// Returns the full path of the executable.
    /// </summary>
    /// <returns>Path of an existing file.</returns>
    /// <exception cref="FileNotFoundException">When no executable is found; the message names every location searched.</exception>
    string Locate();
}
