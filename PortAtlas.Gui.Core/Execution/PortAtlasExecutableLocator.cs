namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Looks for the executable next to the GUI, then in the <c>PORTATLAS_EXE</c> environment variable.
/// </summary>
/// <param name="searchDirectory">Directory of the GUI assembly.</param>
/// <param name="readEnvironmentVariable">Returns the value of an environment variable, or null when it is not set.</param>
public sealed class PortAtlasExecutableLocator(string searchDirectory, Func<string, string?> readEnvironmentVariable) : IPortAtlasExecutableLocator
{
    /// <summary>
    /// Name of the environment variable that points to the executable.
    /// </summary>
    public const string EnvironmentVariableName = "PORTATLAS_EXE";

    /// <summary>
    /// File name of the executable on the current platform.
    /// </summary>
    public static string ExecutableFileName { get; } = OperatingSystem.IsWindows() ? "PortAtlas.exe" : "PortAtlas";

    /// <inheritdoc />
    public string Locate()
    {
        var besideGui = Path.Combine(searchDirectory, ExecutableFileName);
        if (File.Exists(besideGui))
        {
            return besideGui;
        }

        var fromEnvironment = readEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment) && File.Exists(fromEnvironment))
        {
            return fromEnvironment;
        }

        throw new FileNotFoundException(
            $"The PortAtlas executable was not found. Searched: '{besideGui}' and the file named by the {EnvironmentVariableName} environment variable ({(string.IsNullOrWhiteSpace(fromEnvironment) ? "not set" : $"'{fromEnvironment}'")}).");
    }
}
