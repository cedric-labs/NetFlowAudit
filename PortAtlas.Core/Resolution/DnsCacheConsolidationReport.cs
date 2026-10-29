namespace PortAtlas.Core.Resolution;

/// <summary>
/// Aggregated consolidation report containing merged DNS cache entries and recorded conflicts.
/// </summary>
/// <param name="MergedEntries">Deduplicated dictionary of consolidated DNS cache entries keyed by IP address.</param>
/// <param name="Conflicts">List of detected conflicts where multiple caches associated the same IP with distinct hostnames.</param>
/// <param name="TotalCachesProcessed">Number of cache stores ingested during consolidation.</param>
/// <param name="TotalEntriesLoaded">Gross number of raw entries examined across all ingested caches.</param>
public sealed record DnsCacheConsolidationReport(
    IReadOnlyDictionary<string, DnsCacheEntry> MergedEntries,
    IReadOnlyList<DnsCacheConflict> Conflicts,
    int TotalCachesProcessed,
    int TotalEntriesLoaded);
