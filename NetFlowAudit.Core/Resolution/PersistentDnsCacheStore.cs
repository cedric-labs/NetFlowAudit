using System.Text.Json;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Gestionnaire de persistance du cache DNS inverse et métadonnées réseau dans un fichier dédié.
/// </summary>
/// <param name="filePath">Chemin d'accès au fichier de cache JSON.</param>
public sealed class PersistentDnsCacheStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    private readonly object _syncLock = new();

    /// <summary>
    /// Charge les entrées de cache enregistrées depuis le fichier JSON avec rétrocompatibilité des anciens formats plats.
    /// </summary>
    /// <returns>Dictionnaire des correspondances connues, ou un dictionnaire vide si le fichier est absent ou illisible.</returns>
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
                        // Format rétrocompatible plat : { "IP": "HostName" }
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
                        // Nouveau format structuré avec métadonnées complètes
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
                // Toléré : En cas de corruption ou de verrouillage temporaire, le cache mémoire redémarre vierge
                return new Dictionary<string, DnsCacheEntry>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Sauvegarde l'état du cache de manière asynchrone sur le disque.
    /// </summary>
    /// <param name="cache">Dictionnaire des correspondances à persister.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
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
            // Toléré si le fichier est temporairement verrouillé par un processus tiers
        }
    }

    /// <summary>
    /// Surcharge rétrocompatible sauvegardant un dictionnaire simple IP -> Nom d'hôte.
    /// </summary>
    /// <param name="cache">Dictionnaire simple des correspondances à persister.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
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
    /// Sauvegarde l'état du cache de manière synchrone (ex: lors de l'arrêt du service).
    /// </summary>
    /// <param name="cache">Dictionnaire des correspondances à persister.</param>
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
            // Toléré en cas de conflit d'accès
        }
    }

    /// <summary>
    /// Surcharge synchrone rétrocompatible sauvegardant un dictionnaire simple IP -> Nom d'hôte.
    /// </summary>
    /// <param name="cache">Dictionnaire simple des correspondances à persister.</param>
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
