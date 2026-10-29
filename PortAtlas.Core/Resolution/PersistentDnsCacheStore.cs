using System.Text.Json;

namespace PortAtlas.Core.Resolution;

/// <summary>
/// Persistence manager storing reverse DNS and IP organization cache records into a JSON file.
/// </summary>
/// <param name="filePath">Target JSON cache file path.</param>
public sealed class PersistentDnsCacheStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    private readonly object _syncLock = new();

    /// <summary>
    /// Loads persistent cache entries from the JSON file with backward compatibility for legacy flat structures.
    /// </summary>
    /// <returns>Dictionary of known IP-to-entry mappings, or an empty dictionary if missing or unreadable.</returns>
    public Dictionary<string, DnsCacheEntry> Load()
    {
        lock (_syncLock)
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                using var doc = JsonDocument.Parse(json);
                var result = new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);

                if (doc.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return result;
                }

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var ipKey = prop.Name;

                    if (prop.Value.ValueKind == JsonValueKind.String)
                    {
                        // Backward-compatible flat format: { "IP": "HostName" }
                        var name = prop.Value.GetString() ?? string.Empty;
                        result[ipKey] = new DnsCacheEntry
                        {
                            HostName = name,
                            Organization = name.Contains(' ') ? name : null,
                            Source = name.Contains('.') ? "DNS" : (!string.IsNullOrWhiteSpace(name) ? "RDAP" : "None"),
                            ResolvedAtUtc = DateTimeOffset.UtcNow
                        };
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        // Modern structured format with full metadata
                        var entry = prop.Value.Deserialize<DnsCacheEntry>(JsonOptions);
                        if (entry is not null)
                        {
                            result[ipKey] = entry;
                        }
                    }
                }

                return result;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Tolerated: On file corruption or transient locking, start with a clean in-memory cache
                return new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Saves cache entries to disk asynchronously.
    /// </summary>
    /// <param name="cache">Dictionary of IP mappings to persist.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    public async Task SaveAsync(IReadOnlyDictionary<string, DnsCacheEntry> cache, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);

        string json;
        lock (_syncLock)
        {
            json = JsonSerializer.Serialize(cache, JsonOptions);
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(_filePath, json, cancellationToken).ConfigureAwait(false);
        }
        catch (IOException)
        {
            // Tolerated when the file is temporarily locked by another process
        }
    }

    /// <summary>
    /// Backward-compatible overload saving a simple IP-to-hostname dictionary.
    /// </summary>
    /// <param name="cache">Simple IP mappings dictionary to persist.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    public Task SaveAsync(IReadOnlyDictionary<string, string> cache, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(cache);

        var converted = cache.ToDictionary(
            kvp => kvp.Key,
            kvp => new DnsCacheEntry
            {
                HostName = kvp.Value,
                Source = kvp.Value.Contains('.') ? "DNS" : (!string.IsNullOrWhiteSpace(kvp.Value) ? "RDAP" : "None")
            },
            StringComparer.OrdinalIgnoreCase);

        return SaveAsync(converted, cancellationToken);
    }

    /// <summary>
    /// Saves cache entries to disk synchronously (e.g. during application termination).
    /// </summary>
    /// <param name="cache">Dictionary of IP mappings to persist.</param>
    public void Save(IReadOnlyDictionary<string, DnsCacheEntry> cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        string json;
        lock (_syncLock)
        {
            json = JsonSerializer.Serialize(cache, JsonOptions);
        }

        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(_filePath, json);
        }
        catch (IOException)
        {
            // Tolerated on transient access conflict
        }
    }

    /// <summary>
    /// Backward-compatible synchronous overload saving a simple IP-to-hostname dictionary.
    /// </summary>
    /// <param name="cache">Simple IP mappings dictionary to persist.</param>
    public void Save(IReadOnlyDictionary<string, string> cache)
    {
        ArgumentNullException.ThrowIfNull(cache);

        var converted = cache.ToDictionary(
            kvp => kvp.Key,
            kvp => new DnsCacheEntry
            {
                HostName = kvp.Value,
                Source = kvp.Value.Contains('.') ? "DNS" : (!string.IsNullOrWhiteSpace(kvp.Value) ? "RDAP" : "None")
            },
            StringComparer.OrdinalIgnoreCase);

        Save(converted);
    }
}
