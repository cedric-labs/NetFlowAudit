using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using NetFlowAudit.Core.Filtering;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Résolveur d'organisation IP s'appuyant sur le protocole RDAP (RFC 7480/7482/7484) avec disjoncteur (circuit breaker) non bloquant pour environnements isolés.
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
    /// Initialise une nouvelle instance du résolveur RDAP avec configuration optionnelle.
    /// </summary>
    /// <param name="httpClient">Client HTTP injecté (ou null pour utiliser le client interne par défaut).</param>
    /// <param name="requestTimeout">Délai d'attente maximal pour une requête RDAP (2s par défaut).</param>
    /// <param name="circuitBreakerThreshold">Nombre d'échecs réseau consécutifs avant ouverture du disjoncteur (2 par défaut).</param>
    /// <param name="circuitBreakerCooldown">Durée de mise en veille du disjoncteur avant nouvelle tentative (60s par défaut).</param>
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
        ArgumentNullException.ThrowIfNull(address);

        // 1. Filtrage immédiat des adresses privées, locales ou réservées (0ms, aucun trafic réseau)
        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || IPAddress.IsLoopback(address) || PrivateAndReservedMatcher.IsMatch(address))
        {
            return null;
        }

        // 2. Disjoncteur (Circuit Breaker) : en environnement isolé du réseau, évite toute latence réseau cumulative
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
                // Enregistrement non présent dans les RIR (non imputable à un problème de connectivité)
                Interlocked.Exchange(ref _consecutiveFailures, 0);
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
            var org = RdapResponseParser.ExtractOrganization(content);

            // Requête réussie : remise à zéro du compteur de défaillances
            Interlocked.Exchange(ref _consecutiveFailures, 0);
            return org;
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException or TimeoutException or OperationCanceledException)
        {
            // Toléré : Serveur isolé d'Internet, DNS externe inaccessible ou expiration de requête.
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
            // Le disjoncteur est toujours ouvert : échec rapide immédiat sans appel réseau
            return true;
        }

        // Fin de période de refroidissement : on autorise une tentative de sonde
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
