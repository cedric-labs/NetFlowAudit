namespace PortAtlas.Core.Resolution;

/// <summary>
/// Detailed network owner and block allocation metadata resolved via RDAP.
/// </summary>
/// <param name="Organization">Name of the owning organization or entity.</param>
/// <param name="Country">Country code or region (e.g. "US", "FR").</param>
/// <param name="NetworkRange">Allocated network IP address range (e.g. "52.112.0.0 - 52.127.255.255").</param>
/// <param name="Handle">RIR registry allocation handle (e.g. "NET-52-112-0-0-1").</param>
public sealed record IpOwnerMetadata(
    string? Organization,
    string? Country,
    string? NetworkRange,
    string? Handle);
