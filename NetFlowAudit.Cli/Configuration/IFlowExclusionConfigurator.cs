namespace NetFlowAudit.Cli.Configuration;

/// <summary>
/// Contrat de configuration dynamique et de persistance des règles d'exclusion de flux.
/// </summary>
public interface IFlowExclusionConfigurator
{
    /// <summary>
    /// Configuration courante active.
    /// </summary>
    AppConfiguration CurrentConfiguration { get; }

    /// <summary>
    /// Exclut un port local en mémoire et persiste la modification dans le fichier de configuration.
    /// </summary>
    /// <param name="port">Numéro de port local à exclure.</param>
    /// <returns>Description textuelle de l'action effectuée.</returns>
    string ExcludeLocalPort(int port);

    /// <summary>
    /// Exclut une adresse IP ou un sous-réseau distant en mémoire et persiste la modification dans le fichier de configuration.
    /// </summary>
    /// <param name="ipOrCidr">Adresse IP ou plage CIDR distante à exclure.</param>
    /// <returns>Description textuelle de l'action effectuée.</returns>
    string ExcludeRemoteIp(string ipOrCidr);

    /// <summary>
    /// Exclut un port distant en mémoire et persiste la modification dans le fichier de configuration.
    /// </summary>
    /// <param name="port">Numéro de port distant à exclure.</param>
    /// <returns>Description textuelle de l'action effectuée.</returns>
    string ExcludeRemotePort(int port);

    /// <summary>
    /// Exclut un nom exact de processus en mémoire et persiste la modification dans le fichier de configuration.
    /// </summary>
    /// <param name="processName">Nom exact du processus à exclure.</param>
    /// <returns>Description textuelle de l'action effectuée.</returns>
    string ExcludeProcess(string processName);

    /// <summary>
    /// Exclut un exécutable cible ou nom de script en mémoire et persiste la modification dans le fichier de configuration.
    /// </summary>
    /// <param name="target">Cible applicative ou nom d'exécutable à exclure.</param>
    /// <returns>Description textuelle de l'action effectuée.</returns>
    string ExcludeTarget(string target);
}
