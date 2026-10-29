namespace PortAtlas.Core.Export;

/// <summary>
/// Resolves and expands dynamic placeholder tokens in output file paths such as {ServerName}, {Date}, and {Timestamp}.
/// </summary>
public static class OutputFilePathResolver
{
    private static readonly char[] InvalidFileNameCharacters = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Replaces dynamic placeholder tokens ({ServerName}, {HostName}, {Date}, {Timestamp}) within the supplied file path template.
    /// Preserves directory paths while sanitizing dynamic replacement values against illegal filesystem characters.
    /// </summary>
    /// <param name="template">The raw path template containing zero or more placeholder tokens.</param>
    /// <param name="machineName">The target machine or server name (defaults to Environment.MachineName).</param>
    /// <param name="nowUtc">The reference UTC time for date and timestamp resolution (defaults to DateTime.UtcNow).</param>
    /// <returns>The resolved file path with all tokens evaluated and sanitized.</returns>
    public static string Resolve(string template, string? machineName = null, DateTime? nowUtc = null)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return template;
        }

        var effectiveMachineName = string.IsNullOrWhiteSpace(machineName) ? Environment.MachineName : machineName;
        var sanitizedMachineName = SanitizeForFileName(effectiveMachineName);

        var effectiveNowUtc = nowUtc ?? DateTime.UtcNow;
        var formattedDate = effectiveNowUtc.ToString("yyyy-MM-dd");
        var formattedTimestamp = effectiveNowUtc.ToString("yyyyMMdd_HHmmss");

        var result = template;
        result = result.Replace("{ServerName}", sanitizedMachineName, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{HostName}", sanitizedMachineName, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{Date}", formattedDate, StringComparison.OrdinalIgnoreCase);
        result = result.Replace("{Timestamp}", formattedTimestamp, StringComparison.OrdinalIgnoreCase);

        return result;
    }

    /// <summary>
    /// Replaces characters prohibited in file names with a safe dash separator.
    /// </summary>
    /// <param name="value">The raw input string to sanitize.</param>
    /// <returns>A sanitized string safe for file naming.</returns>
    private static string SanitizeForFileName(string value)
    {
        var buffer = value.ToCharArray();
        for (var i = 0; i < buffer.Length; i++)
        {
            if (InvalidFileNameCharacters.Contains(buffer[i]))
            {
                buffer[i] = '-';
            }
        }

        return new string(buffer);
    }
}
