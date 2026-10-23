using SocketForge.Models;

namespace NetFlowAudit.Core.Models;

/// <summary>
/// Enregistrement d'audit consolidé associant une socket active et son contexte d'observation serveur.
/// </summary>
/// <param name="FirstSeenUtc">Date et heure UTC de la première détection.</param>
/// <param name="LastSeenUtc">Date et heure UTC de la dernière détection.</param>
/// <param name="ObservationCount">Nombre de scans successifs où la connexion était active.</param>
/// <param name="Server">Métadonnées du serveur hôte.</param>
/// <param name="Socket">Détails de la socket et du processus résolu via SocketForge et ProcessForge.</param>
/// <param name="RemoteHostName">Nom d'hôte distant résolu par DNS inverse (ou vide si non résolu).</param>
public sealed record NetworkAuditEntry(
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    long ObservationCount,
    ServerHostMetadata Server,
    ActiveSocketConnection Socket,
    string RemoteHostName = "")
{
    /// <summary>
    /// Calcule la clé d'unicité logique de l'entrée d'audit.
    /// </summary>
    public string ComputeAuditKey() => Socket.ComputeLogicalKey();
}
