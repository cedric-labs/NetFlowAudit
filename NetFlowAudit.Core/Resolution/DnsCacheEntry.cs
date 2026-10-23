namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Entrée structurée du cache DNS inverse persistant associant une adresse IP à ses métadonnées de résolution.
/// </summary>
public sealed record DnsCacheEntry
{
    /// <summary>
    /// Nom d'hôte résolu (nom DNS PTR prioritaire, ou organisation RDAP en repli).
    /// </summary>
    public string HostName { get; init; } = string.Empty;

    /// <summary>
    /// Nom de l'organisation détentrice de l'adresse IP (RDAP).
    /// </summary>
    public string? Organization { get; init; }

    /// <summary>
    /// Code pays ou région de l'adresse IP (RDAP).
    /// </summary>
    public string? Country { get; init; }

    /// <summary>
    /// Plage réseau déclarée (RDAP).
    /// </summary>
    public string? NetworkRange { get; init; }

    /// <summary>
    /// Source de la résolution ("DNS", "RDAP", ou "None").
    /// </summary>
    public string Source { get; init; } = "DNS";

    /// <summary>
    /// Horodatage UTC de la résolution.
    /// </summary>
    public DateTimeOffset ResolvedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
