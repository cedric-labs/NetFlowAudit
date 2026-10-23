using System.Text.Json;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Gestionnaire de persistance du cache DNS inverse dans un fichier dédié.
/// </summary>
/// <param name="filePath">Chemin d'accès au fichier de cache JSON.</param>
public sealed class PersistentDnsCacheStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    private readonly object _syncLock = new();

    /// <summary>
    /// Charge les correspondances IP/Nom d'hôte enregistrées depuis le fichier de cache.
    /// </summary>
    /// <returns>Dictionnaire des correspondances connues, ou un dictionnaire vide si le fichier est absent ou illisible.</returns>
    public Dictionary<string, string> Load()
    {
        lock (_syncLock)
        {
            if (!File.Exists(_filePath))
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return loaded is not null
                    ? new Dictionary<string, string>(loaded, StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                // Toléré : En cas de corruption ou de verrouillage temporaire, le cache mémoire redémarre vierge
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
        }
    }

    /// <summary>
    /// Sauvegarde l'état du cache DNS inverse sur le disque de manière asynchrone.
    /// </summary>
    /// <param name="cache">Dictionnaire des correspondances à persister.</param>
    /// <param name="cancellationToken">Jeton d'annulation.</param>
    public async Task SaveAsync(IReadOnlyDictionary<string, string> cache, CancellationToken cancellationToken = default)
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
    /// Sauvegarde l'état du cache de manière synchrone (ex: lors de l'arrêt du service).
    /// </summary>
    /// <param name="cache">Dictionnaire des correspondances à persister.</param>
    public void Save(IReadOnlyDictionary<string, string> cache)
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
}
