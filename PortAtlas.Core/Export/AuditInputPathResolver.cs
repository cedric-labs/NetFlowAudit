namespace PortAtlas.Core.Export;

/// <summary>
/// Expands the audit inputs given on the command line (files, directories or wildcard patterns) into a list of files.
/// </summary>
public static class AuditInputPathResolver
{
    /// <summary>
    /// File extensions of audit logs readable as CSV or NDJSON.
    /// </summary>
    public static readonly IReadOnlyList<string> AuditLogExtensions = [".csv", ".ndjson", ".jsonl"];

    /// <summary>
    /// Resolves each input to the files it designates.
    /// </summary>
    /// <param name="pathsOrPatterns">
    /// File paths, directory paths (every file with one of <paramref name="directoryExtensions"/> in the directory, not recursive)
    /// or wildcard patterns on the file name (<c>audits/nas-*.csv</c>).
    /// </param>
    /// <param name="directoryExtensions">Extensions kept when an input is a directory.</param>
    /// <returns>Distinct full paths of the existing files, in input order; inputs matching nothing are skipped.</returns>
    /// <exception cref="ArgumentNullException">When an argument is null.</exception>
    public static IReadOnlyList<string> Resolve(IEnumerable<string> pathsOrPatterns, IReadOnlyCollection<string> directoryExtensions)
    {
        ArgumentNullException.ThrowIfNull(pathsOrPatterns);
        ArgumentNullException.ThrowIfNull(directoryExtensions);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolved = new List<string>();

        foreach (var file in pathsOrPatterns.Where(p => !string.IsNullOrWhiteSpace(p)).SelectMany(p => Expand(p, directoryExtensions)))
        {
            var fullPath = Path.GetFullPath(file);
            if (seen.Add(fullPath))
            {
                resolved.Add(fullPath);
            }
        }

        return resolved;
    }

    private static IEnumerable<string> Expand(string item, IReadOnlyCollection<string> directoryExtensions)
    {
        if (File.Exists(item))
        {
            return [item];
        }

        if (Directory.Exists(item))
        {
            return directoryExtensions.SelectMany(ext => Directory.EnumerateFiles(item, $"*{ext}", SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase));
        }

        var directory = Path.GetDirectoryName(item);
        var pattern = Path.GetFileName(item);
        if (string.IsNullOrEmpty(directory))
        {
            directory = Directory.GetCurrentDirectory();
        }

        if (!Directory.Exists(directory) || string.IsNullOrWhiteSpace(pattern))
        {
            return [];
        }

        return Directory.EnumerateFiles(directory, pattern, SearchOption.TopDirectoryOnly).Order(StringComparer.OrdinalIgnoreCase);
    }
}
