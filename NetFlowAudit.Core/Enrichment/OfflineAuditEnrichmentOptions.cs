namespace NetFlowAudit.Core.Enrichment;

/// <summary>
/// Configuration options governing offline audit enrichment and consolidation.
/// </summary>
public sealed record OfflineAuditEnrichmentOptions
{
    /// <summary>
    /// When true, overwrites original audit files in-place; otherwise produces files suffixed with '_enriched'.
    /// </summary>
    public bool InPlace { get; init; } = false;

    /// <summary>
    /// When true, forcibly overwrites existing remote host names even if already populated.
    /// </summary>
    public bool ForceResolve { get; init; } = false;

    /// <summary>
    /// When true, strictly restricts resolution to provided cache files without making external DNS or RDAP queries.
    /// </summary>
    public bool Isolated { get; init; } = false;

    /// <summary>
    /// Throttle delay applied between successive live RDAP requests to respect Internet registry rate limits.
    /// </summary>
    public TimeSpan RdapThrottleDelay { get; init; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Target file path where the consolidated master DNS cache should be written.
    /// </summary>
    public string OutputCachePath { get; init; } = "netflow_dns_cache.json";
}
