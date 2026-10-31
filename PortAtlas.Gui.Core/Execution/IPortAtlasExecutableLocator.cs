namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Finds the PortAtlas executable a command is run with.
/// </summary>
public interface IPortAtlasExecutableLocator
{
    /// <summary>
    /// Returns the full path of the executable.
    /// </summary>
    /// <returns>Path of an existing file.</returns>
    /// <exception cref="FileNotFoundException">When no executable is found; the message names every location searched.</exception>
    string Locate();
}
