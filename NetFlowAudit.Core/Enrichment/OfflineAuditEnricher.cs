using System.Net;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using FileForge.IO;
using NetFlowAudit.Core.Export;
using NetFlowAudit.Core.Resolution;

namespace NetFlowAudit.Core.Enrichment;

/// <summary>
/// Orchestrates offline DNS cache consolidation and historical audit enrichment for air-gapped environments.
/// </summary>
/// <param name="consolidator">Consolidator responsible for merging multi-source DNS caches.</param>
/// <param name="offlineResolver">Resolver for executing throttled online DNS and RDAP resolution.</param>
/// <param name="logger">Optional progress reporting callback.</param>
public sealed class OfflineAuditEnricher(
    DnsCacheConsolidator? consolidator = null,
    OfflineDnsResolver? offlineResolver = null,
    Action<string>? logger = null)
{
    private static readonly JsonSerializerOptions CompactJsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false
    };

    private readonly DnsCacheConsolidator _consolidator = consolidator ?? new DnsCacheConsolidator();
    private readonly OfflineDnsResolver _offlineResolver = offlineResolver ?? new OfflineDnsResolver();
    private readonly Action<string>? _logger = logger;

    /// <summary>
    /// Enriches audit files using in-memory or loaded DNS caches.
    /// </summary>
    /// <param name="auditFiles">Collection of CSV or NDJSON audit file paths to enrich.</param>
    /// <param name="initialCaches">Collection of pre-loaded DNS cache dictionaries.</param>
    /// <param name="options">Enrichment and output options.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A comprehensive outcome report containing processed and generated file paths and metrics.</returns>
    public async Task<OfflineAuditEnrichmentReport> EnrichFilesAsync(
        IReadOnlyList<string> auditFiles,
        IReadOnlyList<IReadOnlyDictionary<string, DnsCacheEntry>> initialCaches,
        OfflineAuditEnrichmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditFiles);
        ArgumentNullException.ThrowIfNull(initialCaches);
        ArgumentNullException.ThrowIfNull(options);

        // 1. Consolidate input caches
        var cacheReport = _consolidator.Consolidate(initialCaches);
        var activeCache = new Dictionary<string, DnsCacheEntry>(cacheReport.MergedEntries, StringComparer.OrdinalIgnoreCase);

        // 2. Scan audit files to identify missing IP addresses requiring resolution
        var distinctUnresolvedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var auditFile in auditFiles)
        {
            if (!File.Exists(auditFile))
            {
                continue;
            }

            var ips = await ExtractIpsRequiringResolutionAsync(auditFile, activeCache, options.ForceResolve, cancellationToken).ConfigureAwait(false);
            foreach (var ip in ips)
            {
                distinctUnresolvedIps.Add(ip);
            }
        }

        // 3. Online resolution of missing IPs if not isolated
        var onlineCount = 0;
        if (!options.Isolated && distinctUnresolvedIps.Count > 0)
        {
            _logger?.Invoke($"Résolution en ligne de {distinctUnresolvedIps.Count} adresse(s) IP inconnue(s)...");

            foreach (var ipStr in distinctUnresolvedIps)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (activeCache.ContainsKey(ipStr) && !options.ForceResolve)
                {
                    continue;
                }

                if (IPAddress.TryParse(ipStr, out var address))
                {
                    var resolvedEntry = await _offlineResolver.ResolveSingleAsync(address, cancellationToken).ConfigureAwait(false);
                    activeCache[ipStr] = resolvedEntry;
                    onlineCount++;
                }
            }
        }

        // Persist consolidated master cache if an output cache path is specified
        if (!string.IsNullOrWhiteSpace(options.OutputCachePath))
        {
            var store = new PersistentDnsCacheStore(options.OutputCachePath);
            await store.SaveAsync(activeCache, cancellationToken).ConfigureAwait(false);
            _logger?.Invoke($"Cache consolidé sauvegardé avec succès dans : {options.OutputCachePath}");
        }

        // 4. Enrich audit files
        var processedFiles = new List<string>();
        var generatedFiles = new List<string>();
        var totalScanned = 0;
        var totalUpdated = 0;
        var enrichedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var auditFile in auditFiles)
        {
            if (!File.Exists(auditFile))
            {
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            processedFiles.Add(auditFile);

            var (scanned, updated, outputPath, ipsUpdated) = await EnrichSingleAuditFileAsync(
                auditFile,
                activeCache,
                options,
                cancellationToken).ConfigureAwait(false);

            totalScanned += scanned;
            totalUpdated += updated;
            generatedFiles.Add(outputPath);

            foreach (var ip in ipsUpdated)
            {
                enrichedIps.Add(ip);
            }
        }

        return new OfflineAuditEnrichmentReport(
            processedFiles,
            generatedFiles,
            totalScanned,
            totalUpdated,
            enrichedIps.Count,
            onlineCount,
            cacheReport);
    }

    /// <summary>
    /// Resolves file paths and wildcards, loads cache files, and executes enrichment.
    /// </summary>
    /// <param name="auditPathsOrPatterns">Audit file paths, directories, or glob patterns.</param>
    /// <param name="cachePathsOrPatterns">Cache file paths, directories, or glob patterns.</param>
    /// <param name="options">Enrichment and output options.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A comprehensive outcome report containing processed and generated file paths and metrics.</returns>
    public Task<OfflineAuditEnrichmentReport> EnrichPathsAsync(
        IReadOnlyList<string> auditPathsOrPatterns,
        IReadOnlyList<string> cachePathsOrPatterns,
        OfflineAuditEnrichmentOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(auditPathsOrPatterns);
        ArgumentNullException.ThrowIfNull(cachePathsOrPatterns);
        ArgumentNullException.ThrowIfNull(options);

        var auditFiles = ResolveMatchingFiles(auditPathsOrPatterns, [".csv", ".ndjson", ".jsonl"]);
        var cacheFiles = ResolveMatchingFiles(cachePathsOrPatterns, [".json"]);

        var loadedCaches = new List<IReadOnlyDictionary<string, DnsCacheEntry>>();
        foreach (var cachePath in cacheFiles)
        {
            var store = new PersistentDnsCacheStore(cachePath);
            loadedCaches.Add(store.Load());
        }

        return EnrichFilesAsync(auditFiles, loadedCaches, options, cancellationToken);
    }

    private static IReadOnlyList<string> ResolveMatchingFiles(IEnumerable<string> pathsOrPatterns, string[] validExtensions)
    {
        var resolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in pathsOrPatterns)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                continue;
            }

            if (File.Exists(item))
            {
                resolved.Add(Path.GetFullPath(item));
                continue;
            }

            if (Directory.Exists(item))
            {
                foreach (var ext in validExtensions)
                {
                    foreach (var file in Directory.EnumerateFiles(item, $"*{ext}", SearchOption.TopDirectoryOnly))
                    {
                        resolved.Add(Path.GetFullPath(file));
                    }
                }
                continue;
            }

            var dir = Path.GetDirectoryName(item);
            var pattern = Path.GetFileName(item);
            if (string.IsNullOrEmpty(dir))
            {
                dir = Directory.GetCurrentDirectory();
            }

            if (Directory.Exists(dir) && !string.IsNullOrWhiteSpace(pattern))
            {
                foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                {
                    resolved.Add(Path.GetFullPath(file));
                }
            }
        }

        return resolved.ToList();
    }

    private static async Task<IReadOnlyList<string>> ExtractIpsRequiringResolutionAsync(
        string filePath,
        IReadOnlyDictionary<string, DnsCacheEntry> knownCache,
        bool forceResolve,
        CancellationToken cancellationToken)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var isNdjson = filePath.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase) ||
                       filePath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

        if (isNdjson)
        {
            using var reader = new StreamReader(filePath);
            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    if (doc.RootElement.TryGetProperty("remoteIp", out var ipProp))
                    {
                        var ip = ipProp.GetString();
                        if (string.IsNullOrWhiteSpace(ip))
                        {
                            continue;
                        }

                        doc.RootElement.TryGetProperty("remoteHostName", out var hostProp);
                        var host = hostProp.GetString();

                        if (ShouldResolve(ip, host, knownCache, forceResolve))
                        {
                            result.Add(ip);
                        }
                    }
                }
                catch (JsonException)
                {
                    // Skipped malformed JSON line
                }
            }
        }
        else
        {
            using var reader = new StreamReader(filePath);
            var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (headerLine is null)
            {
                return [];
            }

            var delimiter = headerLine.Contains(';') ? ';' : ',';
            var headers = SplitCsvLine(headerLine, delimiter);
            var (remoteIpIdx, remoteHostIdx) = ResolveCsvColumnIndices(headers);

            if (remoteIpIdx < 0)
            {
                return [];
            }

            string? line;
            while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fields = SplitCsvLine(line, delimiter);
                if (remoteIpIdx < fields.Count)
                {
                    var ip = fields[remoteIpIdx];
                    var host = remoteHostIdx >= 0 && remoteHostIdx < fields.Count ? fields[remoteHostIdx] : string.Empty;

                    if (ShouldResolve(ip, host, knownCache, forceResolve))
                    {
                        result.Add(ip);
                    }
                }
            }
        }

        return result.ToList();
    }

    private static bool ShouldResolve(
        string ip,
        string? host,
        IReadOnlyDictionary<string, DnsCacheEntry> knownCache,
        bool forceResolve)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return false;
        }

        if (IPAddress.TryParse(ip, out var parsedAddr) &&
            (parsedAddr.Equals(IPAddress.Any) || parsedAddr.Equals(IPAddress.IPv6Any) || parsedAddr.Equals(IPAddress.None)))
        {
            return false;
        }

        if (forceResolve)
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(host) || string.Equals(host, ip, StringComparison.OrdinalIgnoreCase))
        {
            return !knownCache.TryGetValue(ip, out var entry) || !string.IsNullOrWhiteSpace(entry.HostName);
        }

        return false;
    }

    private static async Task<(int Scanned, int Updated, string OutputPath, IReadOnlyList<string> EnrichedIps)> EnrichSingleAuditFileAsync(
        string inputPath,
        IReadOnlyDictionary<string, DnsCacheEntry> cache,
        OfflineAuditEnrichmentOptions options,
        CancellationToken cancellationToken)
    {
        var outputPath = options.InPlace ? inputPath : ComputeEnrichedOutputPath(inputPath);
        var tempOutputPath = outputPath + ".tmp_" + Guid.NewGuid().ToString("N");

        var isNdjson = inputPath.EndsWith(".ndjson", StringComparison.OrdinalIgnoreCase) ||
                       inputPath.EndsWith(".jsonl", StringComparison.OrdinalIgnoreCase);

        var scanned = 0;
        var updated = 0;
        var enrichedIps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            if (isNdjson)
            {
                using var reader = new StreamReader(inputPath);
                await using var writer = new StreamWriter(tempOutputPath, append: false);

                string? line;
                while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    scanned++;
                    var (modifiedLine, wasUpdated, ip) = EnrichNdjsonLine(line, cache, options.ForceResolve);
                    if (wasUpdated)
                    {
                        updated++;
                        if (!string.IsNullOrWhiteSpace(ip))
                        {
                            enrichedIps.Add(ip);
                        }
                    }

                    await writer.WriteLineAsync(modifiedLine.AsMemory(), cancellationToken).ConfigureAwait(false);
                }
            }
            else
            {
                using var reader = new StreamReader(inputPath);
                await using var writer = new StreamWriter(tempOutputPath, append: false);

                var headerLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
                if (headerLine is not null)
                {
                    await writer.WriteLineAsync(headerLine.AsMemory(), cancellationToken).ConfigureAwait(false);
                    var delimiter = headerLine.Contains(';') ? ';' : ',';
                    var headers = SplitCsvLine(headerLine, delimiter);
                    var (remoteIpIdx, remoteHostIdx) = ResolveCsvColumnIndices(headers);

                    string? line;
                    while ((line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false)) is not null)
                    {
                        if (string.IsNullOrWhiteSpace(line))
                        {
                            continue;
                        }

                        scanned++;
                        var (modifiedLine, wasUpdated, ip) = EnrichCsvLine(line, delimiter, remoteIpIdx, remoteHostIdx, cache, options.ForceResolve);
                        if (wasUpdated)
                        {
                            updated++;
                            if (!string.IsNullOrWhiteSpace(ip))
                            {
                                enrichedIps.Add(ip);
                            }
                        }

                        await writer.WriteLineAsync(modifiedLine.AsMemory(), cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            File.Move(tempOutputPath, outputPath, overwrite: true);
            return (scanned, updated, outputPath, enrichedIps.ToList());
        }
        finally
        {
            if (File.Exists(tempOutputPath))
            {
                try
                {
                    File.Delete(tempOutputPath);
                }
                catch (IOException)
                {
                    // Tolerated in cleanup
                }
            }
        }
    }

    private static (string ModifiedLine, bool WasUpdated, string? Ip) EnrichNdjsonLine(
        string line,
        IReadOnlyDictionary<string, DnsCacheEntry> cache,
        bool forceResolve)
    {
        try
        {
            var node = JsonNode.Parse(line);
            if (node is not JsonObject obj)
            {
                return (line, false, null);
            }

            if (!obj.TryGetPropertyValue("remoteIp", out var ipNode) || ipNode is null)
            {
                return (line, false, null);
            }

            var ip = ipNode.ToString();
            if (string.IsNullOrWhiteSpace(ip))
            {
                return (line, false, null);
            }

            var currentHost = obj.TryGetPropertyValue("remoteHostName", out var hostNode) ? hostNode?.ToString() : null;

            if (ShouldUpdateHost(ip, currentHost, cache, forceResolve, out var newHost))
            {
                obj["remoteHostName"] = newHost;
                return (obj.ToJsonString(CompactJsonOptions), true, ip);
            }

            return (line, false, null);
        }
        catch (JsonException)
        {
            return (line, false, null);
        }
    }

    private static (string ModifiedLine, bool WasUpdated, string? Ip) EnrichCsvLine(
        string line,
        char delimiter,
        int remoteIpIdx,
        int remoteHostIdx,
        IReadOnlyDictionary<string, DnsCacheEntry> cache,
        bool forceResolve)
    {
        if (remoteIpIdx < 0 || remoteHostIdx < 0)
        {
            return (line, false, null);
        }

        var fields = SplitCsvLine(line, delimiter);
        if (remoteIpIdx >= fields.Count || remoteHostIdx >= fields.Count)
        {
            return (line, false, null);
        }

        var ip = fields[remoteIpIdx];
        var currentHost = fields[remoteHostIdx];

        if (ShouldUpdateHost(ip, currentHost, cache, forceResolve, out var newHost))
        {
            fields[remoteHostIdx] = newHost;
            var escapedFields = fields.Select(f => PersistentDelimitedFileWriter.EscapeField(f, delimiter));
            return (string.Join(delimiter, escapedFields), true, ip);
        }

        return (line, false, null);
    }

    private static bool ShouldUpdateHost(
        string ip,
        string? currentHost,
        IReadOnlyDictionary<string, DnsCacheEntry> cache,
        bool forceResolve,
        out string newHost)
    {
        newHost = string.Empty;

        if (string.IsNullOrWhiteSpace(ip) || !cache.TryGetValue(ip, out var entry))
        {
            return false;
        }

        var resolvedName = !string.IsNullOrWhiteSpace(entry.HostName) ? entry.HostName : (entry.Organization ?? string.Empty);
        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return false;
        }

        if (forceResolve)
        {
            if (!string.Equals(currentHost, resolvedName, StringComparison.OrdinalIgnoreCase))
            {
                newHost = resolvedName;
                return true;
            }
            return false;
        }

        if (string.IsNullOrWhiteSpace(currentHost) || string.Equals(currentHost, ip, StringComparison.OrdinalIgnoreCase))
        {
            newHost = resolvedName;
            return true;
        }

        return false;
    }

    private static string ComputeEnrichedOutputPath(string inputPath)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        return Path.Combine(dir, $"{fileNameWithoutExt}_enriched{ext}");
    }

    private static (int RemoteIpIdx, int RemoteHostIdx) ResolveCsvColumnIndices(IReadOnlyList<string> headers)
    {
        var remoteIpIdx = -1;
        var remoteHostIdx = -1;

        for (var i = 0; i < headers.Count; i++)
        {
            var header = headers[i];
            if (string.Equals(header, "RemoteIp", StringComparison.OrdinalIgnoreCase))
            {
                remoteIpIdx = i;
            }
            else if (string.Equals(header, "RemoteHostName", StringComparison.OrdinalIgnoreCase))
            {
                remoteHostIdx = i;
            }
        }

        // Positional fallbacks for headerless standard 20-column files
        if (remoteIpIdx < 0 && headers.Count >= 20)
        {
            remoteIpIdx = 11;
        }
        if (remoteHostIdx < 0 && headers.Count >= 20)
        {
            remoteHostIdx = 13;
        }

        return (remoteIpIdx, remoteHostIdx);
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var result = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }
}
