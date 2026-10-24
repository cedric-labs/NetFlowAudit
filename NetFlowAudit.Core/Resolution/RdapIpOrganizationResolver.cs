using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetFlowAudit.Core.Filtering;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// IP organization resolver based on RDAP protocol (RFC 7480/7482/7484) with a non-blocking circuit breaker for air-gapped environments.
/// </summary>
public sealed class RdapIpOrganizationResolver : IIpOrganizationResolver, IDisposable
{
    private static readonly IpAddressRangeMatcher PrivateAndReservedMatcher = new(
    [
        "10.0.0.0/8",
        "172.16.0.0/12",
        "192.168.0.0/16",
        "127.0.0.0/8",
        "169.254.0.0/16",
        "100.64.0.0/10",
        "192.0.2.0/24",
        "198.51.100.0/24",
        "203.0.113.0/24",
        "224.0.0.0/4",
        "240.0.0.0/4",
        "0.0.0.0/8",
        "::1/128",
        "::/128",
        "fc00::/7",
        "fe80::/10",
        "ff00::/8"
    ]);

    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly TimeSpan _requestTimeout;
    private readonly int _circuitBreakerThreshold;
    private readonly TimeSpan _circuitBreakerCooldown;

    private int _consecutiveFailures;
    private long _circuitBreakerTripTimestamp;

    /// <summary>
    /// Initializes a new instance of the RDAP resolver with optional configuration overrides.
    /// </summary>
    /// <param name="httpClient">Injected HTTP client (or null to initialize default internal client).</param>
    /// <param name="requestTimeout">Maximum timeout per RDAP request (default: 2s).</param>
    /// <param name="circuitBreakerThreshold">Consecutive failure count before tripping the circuit breaker (default: 2).</param>
    /// <param name="circuitBreakerCooldown">Cooldown period before permitting a trial request after trip (default: 60s).</param>
    public RdapIpOrganizationResolver(
        HttpClient? httpClient = null,
        TimeSpan? requestTimeout = null,
        int circuitBreakerThreshold = 2,
        TimeSpan? circuitBreakerCooldown = null)
    {
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(2);
        _circuitBreakerThreshold = circuitBreakerThreshold;
        _circuitBreakerCooldown = circuitBreakerCooldown ?? TimeSpan.FromSeconds(60);

        if (httpClient is not null)
        {
            _httpClient = httpClient;
            _ownsHttpClient = false;
        }
        else
        {
            var handler = new SocketsHttpHandler
            {
                ConnectTimeout = TimeSpan.FromSeconds(1),
                PooledConnectionLifetime = TimeSpan.FromMinutes(5)
            };

            _httpClient = new HttpClient(handler, disposeHandler: true);
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("NetFlowAudit/1.0");
            _httpClient.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json, application/json");
            _ownsHttpClient = true;
        }
    }

    /// <inheritdoc />
    public async Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        var details = await ResolveOwnerDetailsAsync(address, cancellationToken).ConfigureAwait(false);
        return details?.Organization;
    }

    /// <inheritdoc />
    public async Task<IpOwnerMetadata?> ResolveOwnerDetailsAsync(IPAddress address, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        // 1. Immediate filtering of private, loopback, or reserved IP addresses (0ms, no network traffic)
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || IPAddress.IsLoopback(address) || PrivateAndReservedMatcher.IsMatch(address))
        {
            return null;
        }

        // 2. Circuit breaker check: prevents cumulative network latency in isolated air-gapped environments
        if (IsCircuitOpen())
        {
            return null;
        }

        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_requestTimeout);

            var requestUri = $"https://rdap.org/ip/{address}";
            using var response = await _httpClient.GetAsync(requestUri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                // Record absent from RIR databases (not a network connectivity failure)
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            var details = RdapResponseParser.ExtractOwnerDetails(content);

            // Successful query: reset failure count
            Interlocked.Exchange(ref _consecutiveFailures, 0);
            return details;
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TimeoutException or OperationCanceledException)
        {
            // Tolerated: Server isolated from Internet, external DNS unreachable, or request timed out
            var failures = Interlocked.Increment(ref _consecutiveFailures);
            if (failures >= _circuitBreakerThreshold)
            {
                Interlocked.Exchange(ref _circuitBreakerTripTimestamp, Stopwatch.GetTimestamp());
            }

            return null;
        }
    }

    private bool IsCircuitOpen()
    {
        var trip = Interlocked.Read(ref _circuitBreakerTripTimestamp);
        if (trip == 0)
        {
            return false;
        }

        var elapsedTicks = Stopwatch.GetTimestamp() - trip;
        var elapsed = TimeSpan.FromSeconds((double)elapsedTicks / Stopwatch.Frequency);

        if (elapsed < _circuitBreakerCooldown)
        {
            // Circuit is open: fast-fail immediately without network calls
            return true;
        }

        // Cooldown period elapsed: allow a probe attempt
        Interlocked.Exchange(ref _circuitBreakerTripTimestamp, 0);
        return false;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
