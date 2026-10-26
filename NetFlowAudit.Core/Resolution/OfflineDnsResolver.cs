using System.Net;
using System.Net.Sockets;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Dedicated offline/batch DNS and RDAP resolver providing rate-limiting throttling and progress notifications during mass IP enrichment.
/// </summary>
/// <param name="organizationResolver">Optional IP organization / RDAP resolver.</param>
/// <param name="rdapThrottleDelay">Throttle delay enforced between successive external RDAP queries to protect API quotas.</param>
/// <param name="resolutionTimeout">Timeout per individual DNS PTR lookup.</param>
/// <param name="logThrottlingNotice">Optional notification callback invoked whenever throttling delay is triggered.</param>
public sealed class OfflineDnsResolver(
    IIpOrganizationResolver? organizationResolver = null,
    TimeSpan? rdapThrottleDelay = null,
    TimeSpan? resolutionTimeout = null,
    Action<string>? logThrottlingNotice = null)
{
    private readonly IIpOrganizationResolver? _organizationResolver = organizationResolver;
    private readonly TimeSpan _rdapThrottleDelay = rdapThrottleDelay ?? TimeSpan.FromMilliseconds(200);
    private readonly TimeSpan _resolutionTimeout = resolutionTimeout ?? TimeSpan.FromSeconds(2);
    private readonly Action<string>? _logThrottlingNotice = logThrottlingNotice;

    /// <summary>
    /// Resolves an IP address using reverse DNS PTR followed by throttled RDAP lookup if necessary.
    /// </summary>
    /// <param name="address">Target IP address to resolve.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    /// <returns>A populated DNS cache entry reflecting resolution results.</returns>
    public async Task<DnsCacheEntry> ResolveSingleAsync(
        IPAddress address,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        if (IPAddress.IsLoopback(address))
        {
            return new DnsCacheEntry
            {
                HostName = address.ToString(),
                Source = "Local",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            };
        }

        string? dnsHostName = null;

        // 1. DNS PTR query
        try
        {
            using var dnsTimeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            dnsTimeoutCts.CancelAfter(_resolutionTimeout);

            var entry = await Dns.GetHostEntryAsync(address.ToString(), dnsTimeoutCts.Token).ConfigureAwait(false);
            if (!string.IsNullOrWhiteSpace(entry.HostName) &&
                !string.Equals(entry.HostName, address.ToString(), StringComparison.OrdinalIgnoreCase))
            {
                dnsHostName = entry.HostName;
            }
        }
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException)
        {
            // Expected: Remote address lacks a reverse DNS PTR record or DNS server unreachable
        }

        IpOwnerMetadata? ownerDetails = null;

        // 2. Fallback to RDAP if no PTR found and organization resolver is configured
        if (string.IsNullOrWhiteSpace(dnsHostName) && _organizationResolver is not null)
        {
            if (_rdapThrottleDelay > TimeSpan.Zero)
            {
                _logThrottlingNotice?.Invoke(
                    $"Régulation du débit RDAP active (temporisation {_rdapThrottleDelay.TotalMilliseconds:0}ms pour respecter les quotas des registres Internet)...");
                await Task.Delay(_rdapThrottleDelay, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                ownerDetails = await _organizationResolver.ResolveOwnerDetailsAsync(address, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or SocketException or TimeoutException or OperationCanceledException)
            {
                // Expected: RDAP server unreachable, air-gapped, or cancelled
            }
        }

        string hostName;
        string source;

        if (!string.IsNullOrWhiteSpace(dnsHostName))
        {
            hostName = dnsHostName;
            source = "DNS";
        }
        else if (!string.IsNullOrWhiteSpace(ownerDetails?.Organization))
        {
            hostName = ownerDetails.Organization;
            source = "RDAP";
        }
        else
        {
            hostName = string.Empty;
            source = "None";
        }

        return new DnsCacheEntry
        {
            HostName = hostName,
            Organization = ownerDetails?.Organization,
            Country = ownerDetails?.Country,
            NetworkRange = ownerDetails?.NetworkRange,
            Source = source,
            ResolvedAtUtc = DateTimeOffset.UtcNow
        };
    }
}
