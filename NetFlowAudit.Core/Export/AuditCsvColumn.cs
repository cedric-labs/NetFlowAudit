namespace NetFlowAudit.Core.Export;

/// <summary>
/// Définition des colonnes disponibles pour l'export CSV du journal d'audit de flux réseau.
/// </summary>
public enum AuditCsvColumn
{
    /// <summary>
    /// Date et heure UTC de première observation.
    /// </summary>
    FirstSeenUtc,

    /// <summary>
    /// Date et heure UTC de dernière observation.
    /// </summary>
    LastSeenUtc,

    /// <summary>
    /// Compteur du nombre de fois où le flux a été observé.
    /// </summary>
    ObservationCount,

    /// <summary>
    /// Nom d'hôte du serveur local.
    /// </summary>
    ServerName,

    /// <summary>
    /// Version du système d'exploitation du serveur local.
    /// </summary>
    ServerOsVersion,

    /// <summary>
    /// Adresse IP principale du serveur local.
    /// </summary>
    ServerPrimaryIp,

    /// <summary>
    /// Protocole de transport (TCP, UDP).
    /// </summary>
    Protocol,

    /// <summary>
    /// Direction du flux (Inbound, Outbound, Listening).
    /// </summary>
    Direction,

    /// <summary>
    /// État de la connexion TCP.
    /// </summary>
    TcpState,

    /// <summary>
    /// Adresse IP locale.
    /// </summary>
    LocalIp,

    /// <summary>
    /// Port local.
    /// </summary>
    LocalPort,

    /// <summary>
    /// Adresse IP distante.
    /// </summary>
    RemoteIp,

    /// <summary>
    /// Port distant.
    /// </summary>
    RemotePort,

    /// <summary>
    /// Identifiant numérique du processus (PID).
    /// </summary>
    ProcessId,

    /// <summary>
    /// Nom court de l'exécutable.
    /// </summary>
    ProcessName,

    /// <summary>
    /// Chemin absolu complet du binaire exécutable.
    /// </summary>
    ExecutablePath,

    /// <summary>
    /// Ligne de commande complète de lancement du processus.
    /// </summary>
    CommandLine,

    /// <summary>
    /// Cible résolue (ex: JAR, script exécuté, payload sous-jacent).
    /// </summary>
    TargetExecutable,

    /// <summary>
    /// Compte utilisateur exécutant le processus.
    /// </summary>
    UserName
}
