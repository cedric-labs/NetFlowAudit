using System.Net;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Reverse DNS (PTR) query of one address.
/// </summary>
public interface IReverseDnsLookup
{
    /// <summary>
    /// Returns the host name registered for <paramref name="address"/>.
    /// </summary>
    /// <param name="address">Address to name.</param>
    /// <param name="cancellationToken">Cancelled when the caller's resolution timeout expires or the audit stops.</param>
    /// <returns>The host name, or an empty string when the DNS answer carries none.</returns>
    /// <exception cref="System.Net.Sockets.SocketException">When the DNS servers report no record or cannot be reached.</exception>
    /// <exception cref="OperationCanceledException">When <paramref name="cancellationToken"/> is cancelled.</exception>
    Task<string> LookupAsync(IPAddress address, CancellationToken cancellationToken);
}

/// <summary>
/// Reverse DNS query through the resolver of the operating system.
/// </summary>
public sealed class SystemReverseDnsLookup : IReverseDnsLookup
{
    /// <inheritdoc />
    public async Task<string> LookupAsync(IPAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(address);
        var entry = await Dns.GetHostEntryAsync(address.ToString(), cancellationToken).ConfigureAwait(false);
        return entry.HostName;
    }
}
