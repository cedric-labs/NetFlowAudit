namespace NetFlowAudit.Cli.Dashboard;

/// <summary>
/// Colonnes interactives sélectionnables pour exclusion directe de flux dans le tableau de bord TUI.
/// </summary>
public enum InteractiveExclusionColumn
{
    /// <summary>
    /// Port local d'écoute ou de communication.
    /// </summary>
    Local = 0,

    /// <summary>
    /// Adresse IP ou port distant de communication.
    /// </summary>
    Remote = 1,

    /// <summary>
    /// Nom exact du processus propriétaire.
    /// </summary>
    Process = 2,

    /// <summary>
    /// Exécutable cible ou ligne de commande.
    /// </summary>
    Target = 3
}
