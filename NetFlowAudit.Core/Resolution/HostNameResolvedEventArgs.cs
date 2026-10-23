using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Événement déclenché lorsqu'une adresse IP a été résolue en nom d'hôte par le service DNS en tâche de fond.
/// </summary>
/// <param name="address">Adresse IP résolue.</param>
/// <param name="hostName">Nom d'hôte correspondant (ou chaîne vide si aucun PTR disponible).</param>
public sealed class HostNameResolvedEventArgs(IPAddress address, string hostName) : EventArgs
{
    /// <summary>
    /// Adresse IP résolue.
    /// </summary>
    public IPAddress Address { get; } = address ?? throw new ArgumentNullException(nameof(address));

    /// <summary>
    /// Nom d'hôte correspondant.
    /// </summary>
    public string HostName { get; } = hostName ?? throw new ArgumentNullException(nameof(hostName));
}
