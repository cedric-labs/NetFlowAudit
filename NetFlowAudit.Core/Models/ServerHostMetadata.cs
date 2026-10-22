namespace NetFlowAudit.Core.Models;

/// <summary>
/// Métadonnées d'identification et de localisation du serveur hôte.
/// </summary>
/// <param name="HostName">Nom d'hôte de la machine locale.</param>
/// <param name="OperatingSystemDescription">Description et version du système d'exploitation.</param>
/// <param name="PrimaryIpAddress">Adresse IP principale ou identifiée pour le serveur.</param>
public sealed record ServerHostMetadata(
    string HostName,
    string OperatingSystemDescription,
    string PrimaryIpAddress);
