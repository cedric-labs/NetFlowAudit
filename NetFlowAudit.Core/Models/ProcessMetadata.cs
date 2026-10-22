namespace NetFlowAudit.Core.Models;

/// <summary>
/// Métadonnées détaillées du processus exécutable à l'origine ou cible du flux réseau.
/// </summary>
/// <param name="ProcessId">Identifiant unique du processus (PID).</param>
/// <param name="ProcessName">Nom court du processus (sans chemin ni extension).</param>
/// <param name="ExecutablePath">Chemin complet absolu vers l'exécutable sur disque.</param>
/// <param name="CommandLine">Ligne de commande complète ayant démarré le processus.</param>
/// <param name="TargetExecutable">Cible réelle résolue (ex: JAR, script .bat/ps1, ou exécutable fils pour java/cmd).</param>
/// <param name="UserName">Compte utilisateur exécutant le processus.</param>
public sealed record ProcessMetadata(
    int ProcessId,
    string ProcessName,
    string ExecutablePath,
    string CommandLine,
    string TargetExecutable,
    string UserName);
