namespace NetFlowAudit.Core.Export;

/// <summary>
/// Description descriptive d'une colonne d'audit pour l'aide console et la documentation.
/// </summary>
/// <param name="Column">Identifiant énuméré de la colonne.</param>
/// <param name="Name">Nom technique utilisé dans la configuration et la ligne de commande.</param>
/// <param name="Category">Catégorie thématique de la colonne (Horodatage, Serveur, Réseau, Processus).</param>
/// <param name="Description">Explication détaillée du rôle et du contenu de la colonne.</param>
/// <param name="Example">Exemple typique de valeur observée.</param>
public sealed record AuditCsvColumnDescriptor(
    AuditCsvColumn Column,
    string Name,
    string Category,
    string Description,
    string Example);

/// <summary>
/// Catalogue fournissant la description exhaustive des colonnes CSV et la résolution de configuration.
/// </summary>
public static class AuditCsvColumnCatalog
{
    private static readonly IReadOnlyList<AuditCsvColumnDescriptor> Descriptors =
    [
        new(
            AuditCsvColumn.FirstSeenUtc,
            nameof(AuditCsvColumn.FirstSeenUtc),
            "Horodatage",
            "Date et heure UTC de première observation de la connexion.",
            "2026-10-23T07:15:00.0000000Z"),
        new(
            AuditCsvColumn.LastSeenUtc,
            nameof(AuditCsvColumn.LastSeenUtc),
            "Horodatage",
            "Date et heure UTC de la plus récente observation de la connexion.",
            "2026-10-23T07:20:00.0000000Z"),
        new(
            AuditCsvColumn.ObservationCount,
            nameof(AuditCsvColumn.ObservationCount),
            "Métriques",
            "Nombre cumulé de cycles d'échantillonnage où ce flux a été observé actif.",
            "42"),
        new(
            AuditCsvColumn.ServerName,
            nameof(AuditCsvColumn.ServerName),
            "Serveur",
            "Nom d'hôte du serveur local exécutant l'agent d'audit.",
            "SRV-PROD-APP01"),
        new(
            AuditCsvColumn.ServerOsVersion,
            nameof(AuditCsvColumn.ServerOsVersion),
            "Serveur",
            "Description et version du système d'exploitation de la machine hôte.",
            "Microsoft Windows 10.0.26100"),
        new(
            AuditCsvColumn.ServerPrimaryIp,
            nameof(AuditCsvColumn.ServerPrimaryIp),
            "Serveur",
            "Adresse IP principale routable de la machine hôte.",
            "192.168.1.50"),
        new(
            AuditCsvColumn.Protocol,
            nameof(AuditCsvColumn.Protocol),
            "Réseau",
            "Protocole de transport réseau de la socket (Tcp ou Udp).",
            "Tcp"),
        new(
            AuditCsvColumn.Direction,
            nameof(AuditCsvColumn.Direction),
            "Réseau",
            "Direction déterminée du flux réseau (Listening, Inbound, Outbound).",
            "Outbound"),
        new(
            AuditCsvColumn.TcpState,
            nameof(AuditCsvColumn.TcpState),
            "Réseau",
            "État de la machine à états TCP (Established, Listen, TimeWait, CloseWait, etc.).",
            "Established"),
        new(
            AuditCsvColumn.LocalIp,
            nameof(AuditCsvColumn.LocalIp),
            "Réseau",
            "Adresse IP locale liée à la socket.",
            "192.168.1.50"),
        new(
            AuditCsvColumn.LocalPort,
            nameof(AuditCsvColumn.LocalPort),
            "Réseau",
            "Numéro de port local lié à la socket.",
            "52431"),
        new(
            AuditCsvColumn.RemoteIp,
            nameof(AuditCsvColumn.RemoteIp),
            "Réseau",
            "Adresse IP distante de l'interlocuteur (0.0.0.0 si écoute).",
            "140.82.121.4"),
        new(
            AuditCsvColumn.RemotePort,
            nameof(AuditCsvColumn.RemotePort),
            "Réseau",
            "Numéro de port distant de l'interlocuteur (0 si écoute).",
            "443"),
        new(
            AuditCsvColumn.RemoteHostName,
            nameof(AuditCsvColumn.RemoteHostName),
            "Réseau",
            "Nom d'hôte distant résolu par DNS inverse (ou vide si non résolu).",
            "lb-140-82-121-4-iad.github.com"),
        new(
            AuditCsvColumn.ProcessId,
            nameof(AuditCsvColumn.ProcessId),
            "Processus",
            "Identifiant numérique du processus propriétaire (PID).",
            "4812"),
        new(
            AuditCsvColumn.ProcessName,
            nameof(AuditCsvColumn.ProcessName),
            "Processus",
            "Nom de l'exécutable du processus propriétaire.",
            "git"),
        new(
            AuditCsvColumn.ExecutablePath,
            nameof(AuditCsvColumn.ExecutablePath),
            "Processus",
            "Chemin d'accès absolu complet au binaire exécutable sur le disque.",
            "C:\\Program Files\\Git\\cmd\\git.exe"),
        new(
            AuditCsvColumn.CommandLine,
            nameof(AuditCsvColumn.CommandLine),
            "Processus",
            "Ligne de commande intégrale ayant lancé le processus avec ses arguments.",
            "git fetch --all"),
        new(
            AuditCsvColumn.TargetExecutable,
            nameof(AuditCsvColumn.TargetExecutable),
            "Processus",
            "Cible applicative réelle sous-jacente (ex: archive JAR, script python/shell).",
            "app.jar"),
        new(
            AuditCsvColumn.UserName,
            nameof(AuditCsvColumn.UserName),
            "Processus",
            "Compte utilisateur ou compte de service exécutant le processus.",
            "NT AUTHORITY\\SYSTEM")
    ];

    private static readonly Dictionary<string, AuditCsvColumn> Lookup = Descriptors
        .ToDictionary(d => d.Name, d => d.Column, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Retourne l'ensemble des descripteurs de colonnes disponibles pour l'aide et la documentation.
    /// </summary>
    public static IReadOnlyList<AuditCsvColumnDescriptor> GetAllDescriptors() => Descriptors;

    /// <summary>
    /// Résout une liste de noms de colonnes demandés en énumérations <see cref="AuditCsvColumn"/>.
    /// Si la liste est nulle ou vide, renvoie la sélection par défaut de toutes les colonnes.
    /// </summary>
    /// <param name="requestedColumnNames">Liste optionnelle de noms de colonnes insensibles à la casse.</param>
    /// <returns>La liste ordonnée des colonnes résolues.</returns>
    /// <exception cref="ArgumentException">Lancée lorsqu'un nom de colonne spécifié est inconnu.</exception>
    public static IReadOnlyList<AuditCsvColumn> ResolveColumns(IEnumerable<string>? requestedColumnNames)
    {
        if (requestedColumnNames is null)
        {
            return new AuditCsvOptions().SelectedColumns;
        }

        var list = requestedColumnNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToList();

        if (list.Count == 0)
        {
            return new AuditCsvOptions().SelectedColumns;
        }

        var resolved = new List<AuditCsvColumn>(list.Count);
        foreach (var name in list)
        {
            if (Lookup.TryGetValue(name, out var col))
            {
                resolved.Add(col);
            }
            else
            {
                var availableNames = string.Join(", ", Descriptors.Select(d => d.Name));
                throw new ArgumentException(
                    $"La colonne d'audit '{name}' est inconnue. Colonnes disponibles : {availableNames}",
                    nameof(requestedColumnNames));
            }
        }

        return resolved;
    }
}
