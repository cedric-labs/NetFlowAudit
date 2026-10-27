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

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.None))
        {
            return new DnsCacheEntry
            {
                HostName = string.Empty,
                Source = "Excluded",
                ResolvedAtUtc = DateTimeOffset.UtcNow
            };
        }

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
        catch (Exception ex) when (ex is SocketException or TimeoutException or OperationCanceledException or ArgumentException)
        {
            // Expected: Remote address lacks a reverse DNS PTR record, DNS server unreachable, or invalid host
        }

        IpOwnerMetadata? ownerDetails = null;

        // 2. Query RDAP if organization resolver is configured AND (no PTR found OR address is public routable)
        var organizationResolver = _organizationResolver;
        // 2. Query RDAP if organization resolver is configured AND (no PTR found OR address is public routable)
        if (organizationResolver is not null &&
            (string.IsNullOrWhiteSpace(dnsHostName) || RdapIpOrganizationResolver.IsPublicRoutable(address)))
        {
            if (_rdapThrottleDelay > TimeSpan.Zero && RdapIpOrganizationResolver.IsPublicRoutable(address))
            {
                _logThrottlingNotice?.Invoke(
                    $"RDAP rate limiting active (throttling delay {_rdapThrottleDelay.TotalMilliseconds:0}ms to respect registry quotas)...");
                await Task.Delay(_rdapThrottleDelay, cancellationToken).ConfigureAwait(false);
            }

            try
            {
                ownerDetails = await organizationResolver.ResolveOwnerDetailsAsync(address, cancellationToken).ConfigureAwait(false);
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
