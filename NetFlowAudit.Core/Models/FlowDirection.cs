namespace NetFlowAudit.Core.Models;

/// <summary>
/// Représente la direction d'un flux réseau par rapport au serveur local.
/// </summary>
public enum FlowDirection
{
    /// <summary>
    /// Direction inconnue ou indéterminée.
    /// </summary>
    Unknown,

    /// <summary>
    /// Flux entrant provenant d'une machine distante vers un service local en écoute.
    /// </summary>
    Inbound,

    /// <summary>
    /// Flux sortant initié par le serveur local vers une machine distante.
    /// </summary>
    Outbound,

    /// <summary>
    /// Service local en attente de connexions (Listening).
    /// </summary>
    Listening
}
