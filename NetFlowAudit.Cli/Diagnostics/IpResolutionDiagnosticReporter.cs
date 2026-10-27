using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Resolution;

namespace NetFlowAudit.Cli.Diagnostics;

/// <summary>
/// Diagnostic reporter executing granular, verbose probes for reverse DNS (PTR) and RDAP registration against a target IP address.
/// </summary>
public sealed class IpResolutionDiagnosticReporter
{
    private static readonly HttpClient RdapHttpClient = CreateRdapHttpClient();

    private static HttpClient CreateRdapHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(3),
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
        var client = new HttpClient(handler, disposeHandler: true);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("NetFlowAudit-Diagnostic/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/rdap+json, application/json");
        return client;
    }

    /// <summary>
    /// Executes the complete 6-stage diagnostic pipeline for the target IP address and prints a comprehensive technical report to the console.
    /// </summary>
    /// <param name="targetIpString">The IP string to diagnose.</param>
    /// <param name="config">Application configuration containing cache paths and exclusion rules.</param>
    /// <param name="cancellationToken">Cooperative cancellation token.</param>
    public async Task RunDiagnosticAsync(
        string targetIpString,
        AppConfiguration config,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetIpString);
        ArgumentNullException.ThrowIfNull(config);

        Console.WriteLine("================================================================================");
        Console.WriteLine("           NetFlowAudit - Diagnostic de Résolution IP & Réseau");
        Console.WriteLine("================================================================================");

        // [1/6] Syntax analysis & classification
        Console.WriteLine("\n[1/6] Analyse Syntaxique et Classification");
        Console.WriteLine($"  * Entrée fournie         : {targetIpString}");

        if (!IPAddress.TryParse(targetIpString.Trim(), out var address))
        {
            Console.WriteLine("  * Statut                 : ERREUR - Format d'adresse IP invalide.");
            Console.WriteLine("================================================================================");
            return;
        }

        var isLoopback = IPAddress.IsLoopback(address);
        var isAny = address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any);
        var isNone = address.Equals(IPAddress.None);
        var isPublic = RdapIpOrganizationResolver.IsPublicRoutable(address);
        var exclusionEvaluator = new DnsAddressExclusionEvaluator(config.ExcludedRemoteIps);
        var isConfigExcluded = exclusionEvaluator.ShouldExclude(address);

        Console.WriteLine($"  * Adresse normalisée     : {address} ({address.AddressFamily})");
        Console.WriteLine($"  * Loopback / Local       : {(isLoopback ? "OUI (127.0.0.1 / ::1)" : "NON")}");
        Console.WriteLine($"  * Non spécifiée / Any    : {(isAny ? "OUI (0.0.0.0 / ::)" : "NON")}");
        Console.WriteLine($"  * Broadcast / None       : {(isNone ? "OUI (255.255.255.255)" : "NON")}");
        Console.WriteLine($"  * Routable publiquement  : {(isPublic ? "OUI (Plage WAN / Internet)" : "NON (Privée RFC 1918 / CGNAT / Lien-Local / Réservée)")}");
        Console.WriteLine($"  * Exclue par la config   : {(isConfigExcluded ? "OUI (--exclude-remote-ips)" : "NON")}");

        // [2/6] Cache inspection
        Console.WriteLine($"\n[2/6] Inspection du Cache Local (Fichier: {config.DnsCacheFilePath})");
        DnsCacheEntry? cachedEntry = null;
        if (File.Exists(config.DnsCacheFilePath))
        {
            try
            {
                var cacheStore = new PersistentDnsCacheStore(config.DnsCacheFilePath);
                var loaded = cacheStore.Load();
                if (loaded.TryGetValue(address.ToString(), out cachedEntry))
                {
                    Console.WriteLine("  * Présence dans le cache : OUI");
                    Console.WriteLine($"    - HostName             : {cachedEntry.HostName}");
                    Console.WriteLine($"    - Organisation         : {cachedEntry.Organization ?? "(non renseignée)"}");
                    Console.WriteLine($"    - Pays                 : {cachedEntry.Country ?? "(non renseigné)"}");
                    Console.WriteLine($"    - Plage réseau         : {cachedEntry.NetworkRange ?? "(non renseignée)"}");
                    Console.WriteLine($"    - Source               : {cachedEntry.Source}");
                    Console.WriteLine($"    - Résolu le (UTC)      : {cachedEntry.ResolvedAtUtc:yyyy-MM-dd HH:mm:ss}Z");
                }
                else
                {
                    Console.WriteLine("  * Présence dans le cache : NON (Entrée absente du fichier)");
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                Console.WriteLine($"  * Erreur d'accès cache   : {ex.GetType().Name} - {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("  * Présence dans le cache : NON (Fichier de cache inexistant)");
        }

        // [3/6] System Network Configuration
        Console.WriteLine("\n[3/6] Configuration Réseau & Serveurs DNS de l'OS");
        try
        {
            Console.WriteLine($"  * Nom d'hôte de l'OS     : {Dns.GetHostName()}");
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(nic => nic.OperationalStatus == OperationalStatus.Up &&
                              nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .ToList();

            if (interfaces.Count == 0)
            {
                Console.WriteLine("  * Interfaces actives     : Aucune interface réseau physique active détectée.");
            }
            else
            {
                foreach (var nic in interfaces)
                {
                    var ipProps = nic.GetIPProperties();
                    var dnsServers = ipProps.DnsAddresses.Select(d => d.ToString()).ToList();
                    var suffix = string.IsNullOrWhiteSpace(ipProps.DnsSuffix) ? "(aucun)" : ipProps.DnsSuffix;
                    Console.WriteLine($"  * Interface: {nic.Name} ({nic.Description})");
                    Console.WriteLine($"    - Suffixe DNS local    : {suffix}");
                    Console.WriteLine($"    - Serveurs DNS         : {(dnsServers.Count > 0 ? string.Join(", ", dnsServers) : "(non configurés)")}");
                }
            }
        }
        catch (Exception ex) when (ex is NetworkInformationException or SocketException)
        {
            Console.WriteLine($"  * Erreur lecture réseau  : {ex.Message}");
        }

        // [4/6] Live Reverse DNS (PTR) Probe
        Console.WriteLine("\n[4/6] Test Reverse DNS .NET (PTR)");
        string? reverseDnsResult = null;
        var dnsSw = Stopwatch.StartNew();

        if (isAny || isNone)
        {
            dnsSw.Stop();
            Console.WriteLine("  * Requête Dns.GetHostEntryAsync ignorée (adresse non résoluble/non spécifiée).");
            Console.WriteLine("  * Statut                 : IGNORÉ (Protection préventive)");
        }
        else
        {
            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(3));

                Console.WriteLine($"  * Requête lancée vers   : {address}");
                Console.WriteLine("  * Timeout configuré      : 3000 ms");

                var hostEntry = await Dns.GetHostEntryAsync(address.ToString(), timeoutCts.Token).ConfigureAwait(false);
                dnsSw.Stop();

                Console.WriteLine($"  * Durée de l'appel       : {dnsSw.ElapsedMilliseconds} ms");
                Console.WriteLine("  * Statut                 : SUCCÈS");
                Console.WriteLine($"  * FQDN / HostName retour : {hostEntry.HostName}");
                if (hostEntry.Aliases.Length > 0)
                {
                    Console.WriteLine($"  * Alias retournés        : {string.Join(", ", hostEntry.Aliases)}");
                }
                if (hostEntry.AddressList.Length > 0)
                {
                    Console.WriteLine($"  * Adresses associées     : {string.Join(", ", hostEntry.AddressList.Select(a => a.ToString()))}");
                }

                if (!string.IsNullOrWhiteSpace(hostEntry.HostName) &&
                    !string.Equals(hostEntry.HostName, address.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    reverseDnsResult = hostEntry.HostName;
                }
            }
            catch (SocketException ex)
            {
                dnsSw.Stop();
                Console.WriteLine($"  * Durée de l'appel       : {dnsSw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  * Statut                 : ÉCHEC SocketException (Code: {ex.SocketErrorCode})");
                Console.WriteLine($"  * Message brut           : {ex.Message}");
                Console.WriteLine($"  * Explication            : Le serveur DNS interrogé n'a pas trouvé d'enregistrement PTR ou a répondu NXDOMAIN.");
            }
            catch (OperationCanceledException)
            {
                dnsSw.Stop();
                Console.WriteLine($"  * Durée de l'appel       : {dnsSw.ElapsedMilliseconds} ms");
                Console.WriteLine("  * Statut                 : TIMEOUT / ANNULÉ");
                Console.WriteLine("  * Explication            : Le serveur DNS n'a pas répondu dans le délai imparti (3000 ms).");
            }
            catch (ArgumentException ex)
            {
                dnsSw.Stop();
                Console.WriteLine($"  * Statut                 : ERREUR ArgumentException - {ex.Message}");
            }
        }

        // [5/6] Live RDAP Probe
        Console.WriteLine("\n[5/6] Test RDAP Direct (RFC 7480/7482/7484)");
        IpOwnerMetadata? rdapDetails = null;
        var rdapSw = Stopwatch.StartNew();

        if (!isPublic)
        {
            rdapSw.Stop();
            Console.WriteLine("  * Requête RDAP ignorée   : L'adresse n'est pas une adresse publique routable sur Internet.");
            Console.WriteLine("  * Statut                 : IGNORÉ (Inutile d'interroger les registres RIR pour des IPs locales)");
        }
        else
        {
            var rdapUri = $"https://rdap.org/ip/{address}";
            Console.WriteLine($"  * Requête HTTP lancée    : GET {rdapUri}");
            Console.WriteLine("  * Timeout configuré      : 4000 ms");

            try
            {
                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(4));

                using var response = await RdapHttpClient.GetAsync(rdapUri, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token).ConfigureAwait(false);
                rdapSw.Stop();

                Console.WriteLine($"  * Durée de l'appel       : {rdapSw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  * Code HTTP retourné     : {(int)response.StatusCode} {response.ReasonPhrase}");

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(timeoutCts.Token).ConfigureAwait(false);
                    rdapDetails = RdapResponseParser.ExtractOwnerDetails(content);

                    Console.WriteLine("  * Statut RDAP            : SUCCÈS");
                    Console.WriteLine($"    - Organisation         : {rdapDetails?.Organization ?? "(non trouvée dans le payload JSON)"}");
                    Console.WriteLine($"    - Pays                 : {rdapDetails?.Country ?? "(non renseigné)"}");
                    Console.WriteLine($"    - Plage réseau         : {rdapDetails?.NetworkRange ?? "(non renseignée)"}");
                    Console.WriteLine($"    - Handle RIR           : {rdapDetails?.Handle ?? "(non renseigné)"}");
                }
                else if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    Console.WriteLine("  * Statut RDAP            : 404 NOT FOUND - Aucun enregistrement présent dans les bases RIR (ARIN, RIPE, etc.).");
                }
                else
                {
                    Console.WriteLine($"  * Statut RDAP            : Erreur HTTP {(int)response.StatusCode}");
                }
            }
            catch (HttpRequestException ex)
            {
                rdapSw.Stop();
                Console.WriteLine($"  * Durée de l'appel       : {rdapSw.ElapsedMilliseconds} ms");
                Console.WriteLine($"  * Statut RDAP            : ÉCHEC HttpRequestException - {ex.Message}");
                Console.WriteLine("  * Explication            : Pas d'accès Internet, proxy requis, ou rdap.org indisponible.");
            }
            catch (OperationCanceledException)
            {
                rdapSw.Stop();
                Console.WriteLine($"  * Durée de l'appel       : {rdapSw.ElapsedMilliseconds} ms");
                Console.WriteLine("  * Statut RDAP            : TIMEOUT (Le service rdap.org n'a pas répondu dans les 4000 ms).");
            }
        }

        // [6/6] Synthesis & Pipeline Simulation
        Console.WriteLine("\n[6/6] Synthèse & Modèle Consolidé NetFlowAudit");
        string synthesizedHostName;
        string synthesizedSource;

        if (isLoopback)
        {
            synthesizedHostName = address.ToString();
            synthesizedSource = "Local";
        }
        else if (isAny || isNone || isConfigExcluded)
        {
            synthesizedHostName = string.Empty;
            synthesizedSource = "Excluded";
        }
        else if (!string.IsNullOrWhiteSpace(reverseDnsResult))
        {
            synthesizedHostName = reverseDnsResult;
            synthesizedSource = "DNS";
        }
        else if (!string.IsNullOrWhiteSpace(rdapDetails?.Organization))
        {
            synthesizedHostName = rdapDetails.Organization;
            synthesizedSource = "RDAP";
        }
        else
        {
            synthesizedHostName = string.Empty;
            synthesizedSource = "None";
        }

        Console.WriteLine($"  * HostName retenu        : {(string.IsNullOrEmpty(synthesizedHostName) ? "(aucun)" : synthesizedHostName)}");
        Console.WriteLine($"  * Organisation           : {rdapDetails?.Organization ?? "(aucune)"}");
        Console.WriteLine($"  * Pays                   : {rdapDetails?.Country ?? "(aucun)"}");
        Console.WriteLine($"  * Plage réseau           : {rdapDetails?.NetworkRange ?? "(aucune)"}");
        Console.WriteLine($"  * Source retenue         : {synthesizedSource}");

        if (cachedEntry is not null && !string.Equals(cachedEntry.HostName, synthesizedHostName, StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  * REMARQUE               : Le cache local diffère de la résolution en direct ({cachedEntry.HostName} vs {synthesizedHostName}).");
        }

        Console.WriteLine("================================================================================");
    }
}
