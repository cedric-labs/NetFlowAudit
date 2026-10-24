namespace NetFlowAudit.Core.Models;

/// <summary>
/// Identification and environment metadata of the host server machine.
/// </summary>
/// <param name="HostName">Host name of the local server machine.</param>
/// <param name="OperatingSystemDescription">Operating system description and kernel version.</param>
/// <param name="PrimaryIpAddress">Primary local IP address identified for the server.</param>
public sealed record ServerHostMetadata(
    string HostName,
    string OperatingSystemDescription,
    string PrimaryIpAddress);
