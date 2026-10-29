namespace PortAtlas.Core.Resolution;

/// <summary>
/// Persistent reverse DNS cache entry associating an IP address with resolution metadata.
/// </summary>
public sealed record DnsCacheEntry
{
    /// <summary>
    /// Resolved host name (DNS PTR priority, or RDAP organization fallback).
    /// </summary>
    public string HostName { get; init; } = string.Empty;

    /// <summary>
    /// Organization name owning the IP address (RDAP).
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Country code or region of the IP address (RDAP).
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Allocated network range (RDAP).
    /// </summary>
    public string? NetworkRange { get; init; }

    /// <summary>
    /// Resolution source ("DNS", "RDAP", or "None").
    /// </summary>
    public string Source { get; init; } = "DNS";

    /// <summary>
    /// UTC timestamp of resolution completion.
    /// </summary>
    public DateTimeOffset ResolvedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
