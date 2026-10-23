namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Métadonnées détaillées du propriétaire et du bloc d'adresses réseau résolues via RDAP.
/// </summary>
/// <param name="Organization">Nom de l'organisation ou de l'entité détentrice.</param>
/// <param name="Country">Code pays ou région (ex. "US", "FR").</param>
/// <param name="NetworkRange">Plage d'adresses réseau (ex. "52.112.0.0 - 52.127.255.255").</param>
/// <param name="Handle">Identifiant RIR du bloc réseau (ex. "NET-52-112-0-0-1").</param>
public sealed record IpOwnerMetadata(
    string? Organization,
    string? Country,
    string? NetworkRange,
    string? Handle);
