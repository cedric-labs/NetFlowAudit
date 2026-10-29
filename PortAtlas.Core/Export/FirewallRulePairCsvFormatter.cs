using System.Globalization;
using FileForge.IO;

namespace PortAtlas.Core.Export;

/// <summary>
/// Formats pair matrix rows into RFC 4180 compliant CSV lines.
/// </summary>
public sealed class FirewallRulePairCsvFormatter
{
    private static readonly string[] ColumnNames =
    [
        "PairId",
        "SourceIp",
        "SourceHost",
        "DestinationIp",
        "DestinationHost",
        "FlowCount",
        "Ports",
        "ObservationCount",
        "FirstSeenUtc",
        "LastSeenUtc"
    ];

    /// <summary>
    /// Builds the CSV header line.
    /// </summary>
    /// <param name="delimiter">Field delimiter character.</param>
    /// <returns>Delimited header row string.</returns>
    public string BuildHeader(char delimiter = ',') => string.Join(delimiter, ColumnNames);

    /// <summary>
    /// Formats a pair into an escaped, delimited CSV row.
    /// </summary>
    /// <param name="pair">Pair to serialize.</param>
    /// <param name="delimiter">Field delimiter character.</param>
    /// <returns>Escaped CSV row string.</returns>
    public string FormatRow(FirewallRulePairItem pair, char delimiter = ',')
    {
        ArgumentNullException.ThrowIfNull(pair);

        var values = new[]
        {
            pair.PairId,
            pair.SourceIp,
            pair.SourceHost,
            pair.DestinationIp,
            pair.DestinationHost,
            pair.FlowCount.ToString(CultureInfo.InvariantCulture),
            string.Join(", ", pair.Ports),
            pair.ObservationCount.ToString(CultureInfo.InvariantCulture),
            pair.FirstSeenUtc.ToString("o", CultureInfo.InvariantCulture),
            pair.LastSeenUtc.ToString("o", CultureInfo.InvariantCulture)
        };

        return string.Join(delimiter, values.Select(v => PersistentDelimitedFileWriter.EscapeField(v, delimiter)));
    }
}
