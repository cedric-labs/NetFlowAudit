using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using NetFlowAudit.Core.Models;
using SocketForge.Models;

namespace NetFlowAudit.Cli.Dashboard;

/// <summary>
/// Afficheur de tableau de bord terminal interactif inspiré de btop pour NetFlowAudit.
/// </summary>
public sealed partial class FlowAuditDashboardRenderer
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
    private const string AnsiClearLine = "\x1b[K";

    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly List<NetworkAuditEntry> _recentDiscoveries = [];
    private readonly object _syncLock = new();
    private readonly int _scanIntervalSeconds;
    private readonly string _outputCsvPath;

    private int _lastWindowWidth = -1;
    private int _lastWindowHeight = -1;

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
            CheckAndHandleWindowResize();

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

            var frame = BuildFrame(result, host);

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

    private void CheckAndHandleWindowResize()
    {
        if (Console.IsOutputRedirected)
        {
            return;
        }

        try
        {
            var width = Console.WindowWidth;
            var height = Console.WindowHeight;

            if (_lastWindowWidth != -1 && (width != _lastWindowWidth || height != _lastWindowHeight))
            {
                ClearScreen();
            }

            _lastWindowWidth = width;
            _lastWindowHeight = height;
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Toléré si le terminal ne supporte pas l'interrogation de taille
        }
    }

    private string BuildFrame(FlowAuditCycleResult result, ServerHostMetadata host)
    {
        if (!Console.IsOutputRedirected && _lastWindowWidth > 0 && _lastWindowWidth < 102)
        {
            return BuildWindowTooSmallFrame(_lastWindowWidth);
        }

        return BuildDashboardFrame(result, host);
    }

    internal static string BuildWindowTooSmallFrame(int currentWidth)
    {
        var boxWidth = Math.Clamp(currentWidth - 2, 28, 96);
        var sb = new StringBuilder(512);

        sb.AppendLine($"{AnsiCyan}┌{new string('─', boxWidth)}┐{AnsiReset}{AnsiClearLine}");
        var msg1 = PadRightVisible($" {AnsiYellow}{AnsiBold}Terminal trop étroit : {currentWidth} cols (min: 102 requises){AnsiReset}", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg1}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        var msg2 = PadRightVisible(" Veuillez agrandir la fenêtre du terminal...", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg2}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        sb.AppendLine($"{AnsiCyan}└{new string('─', boxWidth)}┘{AnsiReset}{AnsiClearLine}");
        sb.Append($" {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quitter{AnsiClearLine}");

        return sb.ToString();
    }

    internal string BuildDashboardFrame(FlowAuditCycleResult result, ServerHostMetadata host)
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

        // 1. En-tête principal (Largeur totale : 102 caractères)
        // ┌─ NetFlowAudit v1.0.0 (23) + ─*47 (47) + ┬─ MÉTRIQUES (13) + ─*18 (18) + ┐ (1) = 102
        sb.AppendLine($"{AnsiCyan}┌─ {AnsiBold}{AnsiWhite}NetFlowAudit v1.0.0{AnsiReset}{AnsiCyan} {new string('─', 47)}┬─ {AnsiBold}{AnsiWhite}MÉTRIQUES{AnsiReset}{AnsiCyan} {new string('─', 18)}┐{AnsiReset}{AnsiClearLine}");

        // Lignes du volet d'information avec 3 sous-colonnes parfaitement alignées (22 chars | 23 chars | 18 chars = 69 chars)
        // Ligne 1 : Serveur | OS | Uptime || Sockets actives
        var srvCol1 = $" Serveur : {AnsiWhite}{Truncate(host.HostName, 11),-11}{AnsiReset}";
        var srvCol2 = $"OS : {AnsiDarkGray}{Truncate(host.OperatingSystemDescription, 18),-18}{AnsiReset}";
        var srvCol3 = $"Uptime: {AnsiYellow}{uptimeStr,7}{AnsiReset} ";
        var srvPart = $"{srvCol1} {AnsiDarkGray}│{AnsiReset} {srvCol2} {AnsiDarkGray}│{AnsiReset} {srvCol3}";
        var sockPart = $" Sockets actives : {AnsiGreen}{result.TotalSocketsScanned,6}{AnsiReset}     ";
        sb.AppendLine(FormatSplitRow(srvPart, 69, sockPart, 30));

        // Ligne 2 : IP Hôte | Scan | Heure || Flux uniques
        var ipCol1 = $" IP Hôte : {AnsiWhite}{Truncate(host.PrimaryIpAddress, 11),-11}{AnsiReset}";
        var ipCol2 = $"Scan : {AnsiYellow}{_scanIntervalSeconds}s (auto){AnsiReset}      ";
        var ipCol3 = $"Heure : {AnsiDarkGray}{timestampStr,8}{AnsiReset} ";
        var ipPart = $"{ipCol1} {AnsiDarkGray}│{AnsiReset} {ipCol2} {AnsiDarkGray}│{AnsiReset} {ipCol3}";
        var flowsPart = $" Flux uniques    : {AnsiCyan}{result.TotalUniqueFlowsTracked,6}{AnsiReset}     ";
        sb.AppendLine(FormatSplitRow(ipPart, 69, flowsPart, 30));

        // Ligne 3 : Statut | Filtre | Ignorés || Nouveaux flux
        var stCol1 = " Statut  : Actif      ";
        var stCol2 = "Filtre: Ports/Loopback ";
        var stCol3 = $"Ignorés:{AnsiYellow}{result.ExcludedSocketsCount,5}{AnsiReset} flux";
        var stPart = $"{stCol1} {AnsiDarkGray}│{AnsiReset} {stCol2} {AnsiDarkGray}│{AnsiReset} {stCol3}";
        var newPart = $" Nouveaux flux   : {AnsiMagenta}+{result.NewlyDiscoveredCount,-5}{AnsiReset}    ";
        sb.AppendLine(FormatSplitRow(stPart, 69, newPart, 30));

        // 2. Séparateur vers volets médians (Largeur : 1 + 49 + 1 + 19 + 1 + 30 + 1 = 102)
        sb.AppendLine($"{AnsiCyan}├{new string('─', 49)}┬{new string('─', 19)}┴{new string('─', 30)}┤{AnsiReset}{AnsiClearLine}");

        // 3. Volets médians (Volet trafic 49 chars | Volet processus 50 chars)
        var titleTrafic = $" {AnsiBold}{AnsiWhite}RÉPARTITION DU TRAFIC RÉSEAU{AnsiReset}";
        var titleProc = $" {AnsiBold}{AnsiWhite}TOP PROCESSUS ACTIFS (SOCKETS){AnsiReset}";
        sb.AppendLine(FormatSplitRow(titleTrafic, 49, titleProc, 50));

        var tcpLine = $" TCP : {AnsiGreen}{tcpSockets,4}{AnsiReset} (Écoute: {listeningCount,3}, Sortant: {outboundCount,3})";
        var proc1 = FormatTopProcess(topProcesses, 0, 1);
        sb.AppendLine(FormatSplitRow(tcpLine, 49, proc1, 50));

        var udpLine = $" UDP : {AnsiYellow}{udpSockets,4}{AnsiReset} (Écoute: {udpSockets,3})";
        var proc2 = FormatTopProcess(topProcesses, 1, 2);
        sb.AppendLine(FormatSplitRow(udpLine, 49, proc2, 50));

        var inLine = $" Flux entrants établis : {AnsiCyan}{inboundCount,4}{AnsiReset}";
        var proc3 = FormatTopProcess(topProcesses, 2, 3);
        sb.AppendLine(FormatSplitRow(inLine, 49, proc3, 50));

        var emptyLine = string.Empty;
        var proc4 = FormatTopProcess(topProcesses, 3, 4);
        sb.AppendLine(FormatSplitRow(emptyLine, 49, proc4, 50));

        // 4. Séparateur vers tableau des découvertes (1 + 49 + 1 + 50 + 1 = 102)
        sb.AppendLine($"{AnsiCyan}├{new string('─', 49)}┴{new string('─', 50)}┤{AnsiReset}{AnsiClearLine}");

        // 5. Titre et colonnes du tableau des découvertes (Intérieur : 100 caractères)
        var tableTitle = $" {AnsiBold}{AnsiWhite}DERNIÈRES DÉCOUVERTES DE FLUX (TEMPS RÉEL){AnsiReset}";
        sb.AppendLine(FormatRow(tableTitle, 100));

        var tableHeader = $" {AnsiDarkGray}HEURE (UTC)  PROTO DIR  LOCAL                 DISTANT               PROCESSUS   CIBLE / CMD{AnsiReset}";
        sb.AppendLine(FormatRow(tableHeader, 100));

        for (var i = 0; i < 8; i++)
        {
            if (i < _recentDiscoveries.Count)
            {
                var entry = _recentDiscoveries[i];
                var time = entry.FirstSeenUtc.ToString("HH:mm:ss");
                var proto = entry.Socket.Protocol == SocketTransportProtocol.Tcp ? "TCP " : "UDP ";
                var dir = entry.Socket.Direction switch
                {
                    SocketFlowDirection.Listening => "LIST",
                    SocketFlowDirection.Inbound => "IN  ",
                    SocketFlowDirection.Outbound => "OUT ",
                    _ => "UNKN"
                };

                var local = Truncate($"{entry.Socket.LocalEndpoint.Address}:{entry.Socket.LocalEndpoint.Port}", 21);
                var remote = Truncate($"{entry.Socket.RemoteEndpoint.Address}:{entry.Socket.RemoteEndpoint.Port}", 21);
                var proc = Truncate(entry.Socket.Process.ProcessName, 12);
                var target = Truncate(!string.IsNullOrWhiteSpace(entry.Socket.Process.TargetExecutable)
                    ? entry.Socket.Process.TargetExecutable
                    : entry.Socket.Process.CommandLine, 22);

                var color = entry.Socket.Protocol == SocketTransportProtocol.Tcp ? AnsiGreen : AnsiYellow;
                var row = $" {AnsiDarkGray}{time}{AnsiReset}  {color}{proto}{AnsiReset} {dir} {local,-21} {remote,-21} {AnsiMagenta}{proc,-12}{AnsiReset} {AnsiWhite}{target,-22}{AnsiReset}";
                sb.AppendLine(FormatRow(row, 100));
            }
            else
            {
                sb.AppendLine(FormatRow(string.Empty, 100));
            }
        }

        // 6. Pied de page (1 + 100 + 1 = 102)
        sb.AppendLine($"{AnsiCyan}└{new string('─', 100)}┘{AnsiReset}{AnsiClearLine}");
        sb.Append($" {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quitter  {AnsiBold}{AnsiYellow}[C]{AnsiReset} Effacer  {AnsiDarkGray}| Journal : {_outputCsvPath} (RFC 4180){AnsiReset}{AnsiClearLine}");

        return sb.ToString();
    }

    private static string FormatRow(string content, int targetWidth)
    {
        return $"{AnsiCyan}│{AnsiReset}{PadRightVisible(content, targetWidth)}{AnsiCyan}│{AnsiReset}{AnsiClearLine}";
    }

    private static string FormatSplitRow(string col1, int col1Width, string col2, int col2Width)
    {
        var left = PadRightVisible(col1, col1Width);
        var right = PadRightVisible(col2, col2Width);
        return $"{AnsiCyan}│{AnsiReset}{left}{AnsiCyan}│{AnsiReset}{right}{AnsiCyan}│{AnsiReset}{AnsiClearLine}";
    }

    private static string FormatTopProcess(List<KeyValuePair<string, int>> list, int index, int rank)
    {
        if (index >= list.Count)
        {
            return $" {rank}. ---";
        }

        var item = list[index];
        var name = Truncate(item.Key, 28);
        return $" {rank}. {name,-28} ({item.Value,3} sockets)";
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

    private static string PadRightVisible(string text, int targetWidth)
    {
        var visibleLen = GetVisibleWidth(text);
        if (visibleLen >= targetWidth)
        {
            return text;
        }

        return text + new string(' ', targetWidth - visibleLen);
    }

    internal static int GetVisibleWidth(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var stripped = AnsiEscapeRegex().Replace(text, string.Empty);
        return stripped.Length;
    }

    [GeneratedRegex(@"\x1B\[[0-?]*[ -/]*[@-~]")]
    private static partial Regex AnsiEscapeRegex();
}
