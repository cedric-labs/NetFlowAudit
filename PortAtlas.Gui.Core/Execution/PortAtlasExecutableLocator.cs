namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Looks for the executable at the configured path, next to the GUI, then in the <c>PORTATLAS_EXE</c> environment variable.
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
    public string? ConfiguredPath { get; set; }

    /// <inheritdoc />
    public bool TryLocate(out string? path)
    {
        path = Candidates().FirstOrDefault(File.Exists);
        return path is not null;
    }

    /// <inheritdoc />
    public string Locate()
    {
        if (TryLocate(out var path) && path is not null)
        {
            return path;
        }

        var fromEnvironment = readEnvironmentVariable(EnvironmentVariableName);
        var configured = string.IsNullOrWhiteSpace(ConfiguredPath) ? string.Empty : $"the configured path '{ConfiguredPath}', ";
        throw new FileNotFoundException(
            $"The PortAtlas executable was not found. Searched: {configured}'{BesideGuiPath()}' and the file named by the {EnvironmentVariableName} environment variable ({(string.IsNullOrWhiteSpace(fromEnvironment) ? "not set" : $"'{fromEnvironment}'")}).");
    }

    private IEnumerable<string> Candidates()
    {
        if (!string.IsNullOrWhiteSpace(ConfiguredPath))
        {
            yield return ConfiguredPath;
        }

        yield return BesideGuiPath();

        var fromEnvironment = readEnvironmentVariable(EnvironmentVariableName);
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            yield return fromEnvironment;
        }
    }

    private string BesideGuiPath() => Path.Combine(searchDirectory, ExecutableFileName);
}
