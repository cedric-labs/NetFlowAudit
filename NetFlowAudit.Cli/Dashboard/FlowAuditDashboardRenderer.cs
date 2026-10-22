using System.Diagnostics;
using System.Text;
using NetFlowAudit.Core.Models;
using SocketForge.Models;

namespace NetFlowAudit.Cli.Dashboard;

/// <summary>
/// Afficheur de tableau de bord terminal interactif inspiré de btop pour NetFlowAudit.
/// </summary>
public sealed class FlowAuditDashboardRenderer
{
    private const string AnsiReset = "\x1b[0m";
    private const string AnsiBold = "\x1b[1m";
    private const string AnsiCyan = "\x1b[36m";
    private const string AnsiGreen = "\x1b[32m";
    private const string AnsiYellow = "\x1b[33m";
    private const string AnsiMagenta = "\x1b[35m";
    private const string AnsiDarkGray = "\x1b[90m";
    private const string AnsiWhite = "\x1b[97m";
    private const string AnsiHome = "\x1b[H";

    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly List<NetworkAuditEntry> _recentDiscoveries = [];
    private readonly object _syncLock = new();
    private readonly int _scanIntervalSeconds;
    private readonly string _outputCsvPath;

    /// <summary>
    /// Initialise le moteur de rendu du tableau de bord.
    /// </summary>
    /// <param name="scanIntervalSeconds">Intervalle de scan configuré.</param>
    /// <param name="outputCsvPath">Chemin du fichier CSV de sortie.</param>
    public FlowAuditDashboardRenderer(int scanIntervalSeconds, string outputCsvPath)
    {
        _scanIntervalSeconds = scanIntervalSeconds;
        _outputCsvPath = outputCsvPath;

        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.CursorVisible = false;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Environnement sans contrôle de curseur
        }
    }

    /// <summary>
    /// Réinitialise l'affichage console.
    /// </summary>
    public void ClearScreen()
    {
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // Erreur console tolérée
        }
    }

    /// <summary>
    /// Restaure la visibilité du curseur à la fermeture.
    /// </summary>
    public void RestoreCursor()
    {
        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.CursorVisible = true;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Toléré lors de la fermeture
        }
    }

    /// <summary>
    /// Met à jour et affiche la trame courante du tableau de bord.
    /// </summary>
    /// <param name="result">Synthèse métrique du dernier scan.</param>
    /// <param name="host">Métadonnées de la machine hôte.</param>
    public void Render(FlowAuditCycleResult result, ServerHostMetadata host)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(host);

        lock (_syncLock)
        {
            if (result.NewlyDiscoveredEntries.Count > 0)
            {
                foreach (var entry in result.NewlyDiscoveredEntries)
                {
                    _recentDiscoveries.Insert(0, entry);
                }

                while (_recentDiscoveries.Count > 8)
                {
                    _recentDiscoveries.RemoveAt(_recentDiscoveries.Count - 1);
                }
            }

            var frame = BuildDashboardFrame(result, host);

            try
            {
                Console.Write(AnsiHome + frame);
            }
            catch (IOException)
            {
                // Erreur d'écriture console (ex: arrêt du terminal)
            }
        }
    }

    private string BuildDashboardFrame(FlowAuditCycleResult result, ServerHostMetadata host)
    {
        var sb = new StringBuilder(4096);

        var uptimeStr = FormatUptime(_uptime.Elapsed);
        var timestampStr = DateTime.Now.ToString("HH:mm:ss");

        var tcpSockets = 0;
        var udpSockets = 0;
        var listeningCount = 0;
        var outboundCount = 0;
        var inboundCount = 0;

        var processCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var s in result.ActiveSockets)
        {
            if (s.Protocol == SocketTransportProtocol.Tcp)
            {
                tcpSockets++;
            }
            else
            {
                udpSockets++;
            }

            switch (s.Direction)
            {
                case SocketFlowDirection.Listening:
                    listeningCount++;
                    break;
                case SocketFlowDirection.Outbound:
                    outboundCount++;
                    break;
                case SocketFlowDirection.Inbound:
                    inboundCount++;
                    break;
            }

            var procName = string.IsNullOrWhiteSpace(s.Process.ProcessName) ? "Inconnu" : s.Process.ProcessName;
            processCounts[procName] = processCounts.GetValueOrDefault(procName) + 1;
        }

        var topProcesses = processCounts
            .OrderByDescending(kv => kv.Value)
            .Take(4)
            .ToList();

        // 1. En-tête principal
        sb.AppendLine($"{AnsiCyan}┌─ {AnsiBold}{AnsiWhite}NetFlowAudit v1.0.0{AnsiReset}{AnsiCyan} ────────────────────────────────────────────────────────┬─ {AnsiBold}{AnsiWhite}MÉTRIQUES{AnsiReset}{AnsiCyan} ─────────────┐{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} Serveur : {AnsiWhite}{Truncate(host.HostName, 18),-18}{AnsiReset} | OS : {AnsiDarkGray}{Truncate(host.OperatingSystemDescription, 20),-20}{AnsiReset} | Uptime: {AnsiYellow}{uptimeStr,8}{AnsiReset} {AnsiCyan}│{AnsiReset} Sockets actives : {AnsiGreen}{result.TotalSocketsScanned,5}{AnsiReset} {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} IP Hôte : {AnsiWhite}{Truncate(host.PrimaryIpAddress, 18),-18}{AnsiReset} | Scan: {AnsiYellow}{_scanIntervalSeconds}s{AnsiReset}              | Heure : {AnsiDarkGray}{timestampStr,8}{AnsiReset} {AnsiCyan}│{AnsiReset} Flux uniques    : {AnsiCyan}{result.TotalUniqueFlowsTracked,5}{AnsiReset} {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} Exclusions : {AnsiYellow}{result.ExcludedSocketsCount,5}{AnsiReset} sockets ignorées (ports & loopback)                   {AnsiCyan}│{AnsiReset} Nouveaux flux   : {AnsiMagenta}+{result.NewlyDiscoveredCount,-4}{AnsiReset} {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}├───────────────────────────────────────────────┬───────────────────────────────┴───────────────────────┤{AnsiReset}");

        // 2. Volets médians
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} {AnsiBold}{AnsiWhite}RÉPARTITION DU TRAFIC RÉSEAU{AnsiReset}                  {AnsiCyan}│{AnsiReset} {AnsiBold}{AnsiWhite}TOP PROCESSUS ACTIFS (SOCKETS){AnsiReset}                 {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} TCP : {AnsiGreen}{tcpSockets,4}{AnsiReset} (Écoute: {listeningCount,3}, Sortant: {outboundCount,3})      {AnsiCyan}│{AnsiReset} 1. {FormatTopProcess(topProcesses, 0)} {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} UDP : {AnsiYellow}{udpSockets,4}{AnsiReset} (Écoute: {udpSockets,3})                      {AnsiCyan}│{AnsiReset} 2. {FormatTopProcess(topProcesses, 1)} {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} Flux entrants établis : {inboundCount,4}                     {AnsiCyan}│{AnsiReset} 3. {FormatTopProcess(topProcesses, 2)} {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}                                                {AnsiCyan}│{AnsiReset} 4. {FormatTopProcess(topProcesses, 3)} {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}├───────────────────────────────────────────────┴───────────────────────────────────────────────────────┤{AnsiReset}");

        // 3. Tableau des dernières découvertes
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} {AnsiBold}{AnsiWhite}DERNIÈRES DÉCOUVERTES DE FLUX (TEMPS RÉEL){AnsiReset}                                                  {AnsiCyan}│{AnsiReset}");
        sb.AppendLine($"{AnsiCyan}│{AnsiReset} {AnsiDarkGray}HEURE (UTC)  PROTO DIR    LOCAL                DISTANT              PROCESSUS  CIBLE / CMD{AnsiReset}  {AnsiCyan}│{AnsiReset}");

        for (var i = 0; i < 8; i++)
        {
            if (i < _recentDiscoveries.Count)
            {
                var entry = _recentDiscoveries[i];
                var time = entry.FirstSeenUtc.ToString("HH:mm:ss");
                var proto = entry.Socket.Protocol == SocketTransportProtocol.Tcp ? "TCP" : "UDP";
                var dir = entry.Socket.Direction switch
                {
                    SocketFlowDirection.Listening => "LIST",
                    SocketFlowDirection.Inbound => "IN  ",
                    SocketFlowDirection.Outbound => "OUT ",
                    _ => "UNKN"
                };

                var local = Truncate($"{entry.Socket.LocalEndpoint.Address}:{entry.Socket.LocalEndpoint.Port}", 20);
                var remote = Truncate($"{entry.Socket.RemoteEndpoint.Address}:{entry.Socket.RemoteEndpoint.Port}", 20);
                var proc = Truncate(entry.Socket.Process.ProcessName, 10);
                var target = Truncate(!string.IsNullOrWhiteSpace(entry.Socket.Process.TargetExecutable)
                    ? entry.Socket.Process.TargetExecutable
                    : entry.Socket.Process.CommandLine, 24);

                var color = entry.Socket.Protocol == SocketTransportProtocol.Tcp ? AnsiGreen : AnsiYellow;
                var line = $"│ {AnsiDarkGray}{time}{AnsiReset}  {color}{proto,-5}{AnsiReset} {dir} {local,-20} {remote,-20} {AnsiMagenta}{proc,-10}{AnsiReset} {AnsiWhite}{target,-24}{AnsiReset} {AnsiCyan}│{AnsiReset}";
                sb.AppendLine(line);
            }
            else
            {
                sb.AppendLine($"{AnsiCyan}│{AnsiReset}                                                                                        {AnsiCyan}│{AnsiReset}");
            }
        }

        // 4. Pied de page
        sb.AppendLine($"{AnsiCyan}└───────────────────────────────────────────────────────────────────────────────────────┘{AnsiReset}");
        sb.Append($" {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quitter  {AnsiBold}{AnsiYellow}[C]{AnsiReset} Effacer  {AnsiDarkGray}| Journal : {_outputCsvPath} (RFC 4180){AnsiReset}");

        return sb.ToString();
    }

    private static string FormatTopProcess(List<KeyValuePair<string, int>> list, int index)
    {
        if (index >= list.Count)
        {
            return "---                                  ";
        }

        var item = list[index];
        var name = Truncate(item.Key, 24);
        var count = item.Value;
        return $"{name,-24} ({count,3} sockets)";
    }

    private static string FormatUptime(TimeSpan ts)
    {
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours:00}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:00}:{ts.Seconds:00}";
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return value.Length <= maxLength ? value : value[..(maxLength - 1)] + "…";
    }
}
