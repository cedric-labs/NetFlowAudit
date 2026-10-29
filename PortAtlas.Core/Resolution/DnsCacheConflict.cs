namespace PortAtlas.Core.Resolution;

/// <summary>
/// Represents a conflict discovered during DNS cache consolidation when an IP address maps to different hostnames.
/// </summary>
/// <param name="IpAddress">The conflicting IP address.</param>
/// <param name="ConflictingHostName">The rejected hostname value.</param>
/// <param name="WinningHostName">The accepted hostname value selected by the consolidation heuristic.</param>
/// <param name="Reason">Explanation detailing why the winning hostname was chosen.</param>
public sealed record DnsCacheConflict(
    string IpAddress,
    string ConflictingHostName,
    string WinningHostName,
    string Reason);
