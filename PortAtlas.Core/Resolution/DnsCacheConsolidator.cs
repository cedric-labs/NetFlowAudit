namespace PortAtlas.Core.Resolution;

/// <summary>
/// Consolidates multiple DNS cache sources into a single deduplicated mapping, arbitrating conflicting hostnames and tracking anomalies.
/// </summary>
public sealed class DnsCacheConsolidator
{
    /// <summary>
    /// Merges multiple DNS cache dictionaries into a consolidated report.
    /// </summary>
    /// <param name="caches">Collection of DNS cache dictionaries to merge.</param>
    /// <returns>A consolidation report containing deduplicated entries and detected conflicts.</returns>
    public DnsCacheConsolidationReport Consolidate(IEnumerable<IReadOnlyDictionary<string, DnsCacheEntry>> caches)
    {
        ArgumentNullException.ThrowIfNull(caches);

        var merged = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);
        var conflicts = new List<DnsCacheConflict>();
        var totalCaches = 0;
        var totalLoaded = 0;

        foreach (var cache in caches)
        {
            totalCaches++;
            foreach (var (ip, incomingEntry) in cache)
            {
                totalLoaded++;
                if (string.IsNullOrWhiteSpace(ip) || incomingEntry is null)
                {
                    continue;
                }

                if (!merged.TryGetValue(ip, out var existingEntry))
                {
                    merged[ip] = incomingEntry;
                    continue;
                }

                if (string.Equals(existingEntry.HostName, incomingEntry.HostName, StringComparison.OrdinalIgnoreCase))
                {
                    merged[ip] = MergeMatchingEntries(existingEntry, incomingEntry);
                    continue;
                }

                var (winner, loser, reason) = ArbitrateConflict(ip, existingEntry, incomingEntry);
                merged[ip] = winner;
                conflicts.Add(new DnsCacheConflict(ip, loser.HostName, winner.HostName, reason));
            }
        }

        return new DnsCacheConsolidationReport(merged, conflicts, totalCaches, totalLoaded);
    }

    /// <summary>
    /// Loads and consolidates multiple cache files from disk.
    /// </summary>
    /// <param name="cacheFilePaths">Paths to the JSON cache files to ingest.</param>
    /// <returns>A consolidation report containing deduplicated entries and detected conflicts.</returns>
    public DnsCacheConsolidationReport ConsolidateFiles(IEnumerable<string> cacheFilePaths)
    {
        ArgumentNullException.ThrowIfNull(cacheFilePaths);

        var caches = new List<IReadOnlyDictionary<string, DnsCacheEntry>>();

        foreach (var path in cacheFilePaths)
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var store = new PersistentDnsCacheStore(path);
            var loaded = store.Load();
            caches.Add(loaded);
        }

        return Consolidate(caches);
    }

    private static DnsCacheEntry MergeMatchingEntries(DnsCacheEntry existing, DnsCacheEntry incoming)
    {
        var pickLatestTime = incoming.ResolvedAtUtc > existing.ResolvedAtUtc ? incoming.ResolvedAtUtc : existing.ResolvedAtUtc;
        return new DnsCacheEntry
        {
            HostName = existing.HostName,
            Organization = existing.Organization ?? incoming.Organization,
            Country = existing.Country ?? incoming.Country,
            NetworkRange = existing.NetworkRange ?? incoming.NetworkRange,
            Source = string.Equals(existing.Source, "DNS", StringComparison.OrdinalIgnoreCase) ? existing.Source : incoming.Source,
            ResolvedAtUtc = pickLatestTime
        };
    }

    private static (DnsCacheEntry Winner, DnsCacheEntry Loser, string Reason) ArbitrateConflict(
        string ip,
        DnsCacheEntry existing,
        DnsCacheEntry incoming)
    {
        var existingIsDns = string.Equals(existing.Source, "DNS", StringComparison.OrdinalIgnoreCase);
        var incomingIsDns = string.Equals(incoming.Source, "DNS", StringComparison.OrdinalIgnoreCase);

        if (existingIsDns && !incomingIsDns)
        {
            return (existing, incoming, $"Retained DNS PTR '{existing.HostName}' over RDAP organization '{incoming.HostName}'");
        }

        if (incomingIsDns && !existingIsDns)
        {
            return (incoming, existing, $"Prioritized DNS PTR '{incoming.HostName}' over RDAP organization '{existing.HostName}'");
        }

        if (incoming.ResolvedAtUtc > existing.ResolvedAtUtc)
        {
            return (incoming, existing, $"Selected more recent resolution timestamp ({incoming.ResolvedAtUtc:u} > {existing.ResolvedAtUtc:u})");
        }

        return (existing, incoming, $"Retained prior resolution timestamp ({existing.ResolvedAtUtc:u} >= {incoming.ResolvedAtUtc:u})");
    }
}
