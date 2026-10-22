namespace NetFlowAudit.Core.Models;

/// <summary>
/// Enregistrement complet et immuable représentant une connexion réseau auditée.
/// </summary>
/// <param name="FirstSeenUtc">Date et heure UTC de la première observation du flux.</param>
/// <param name="LastSeenUtc">Date et heure UTC de la dernière observation du flux.</param>
/// <param name="ObservationCount">Nombre d'occurrences ou de scans consécutifs où ce flux a été observé.</param>
/// <param name="Server">Métadonnées du serveur hôte.</param>
/// <param name="Protocol">Protocole de transport (TCP, UDP).</param>
/// <param name="Direction">Direction déterminée du flux (Entrant, Sortant, Écoute).</param>
/// <param name="LocalEndpoint">Point de terminaison réseau local.</param>
/// <param name="RemoteEndpoint">Point de terminaison réseau distant (IP:0 pour une écoute).</param>
/// <param name="TcpState">État de la connexion TCP (ex: Established, Listen, TimeWait, etc.).</param>
/// <param name="Process">Métadonnées du processus propriétaire de la socket.</param>
public sealed record NetworkConnectionRecord(
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    long ObservationCount,
    ServerHostMetadata Server,
    NetworkTransportProtocol Protocol,
    FlowDirection Direction,
    NetworkEndpoint LocalEndpoint,
    NetworkEndpoint RemoteEndpoint,
    string TcpState,
    ProcessMetadata Process)
{
    /// <summary>
    /// Calcule une clé de dédoublonnage canonique basée sur l'identité applicative et logique du flux.
    /// Pour les flux sortants, le port local dynamique (éphémère) est ignoré par conception.
    /// </summary>
    public string ComputeDeduplicationKey()
    {
        var targetOrPath = string.IsNullOrWhiteSpace(Process.TargetExecutable)
            ? Process.ExecutablePath
            : Process.TargetExecutable;

        return Direction switch
        {
            FlowDirection.Outbound => $"{Protocol}:{Direction}:{targetOrPath}:{RemoteEndpoint.Address}:{RemoteEndpoint.Port}",
            FlowDirection.Inbound => $"{Protocol}:{Direction}:{targetOrPath}:{LocalEndpoint.Port}:{RemoteEndpoint.Address}",
            FlowDirection.Listening => $"{Protocol}:{Direction}:{targetOrPath}:{LocalEndpoint.Port}",
            _ => $"{Protocol}:{Direction}:{targetOrPath}:{LocalEndpoint}:{RemoteEndpoint}"
        };
    }
}
