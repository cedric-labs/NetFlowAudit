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
    private const string AnsiEnterAltBuffer = "\x1b[?1049h\x1b[H\x1b[2J";
    private const string AnsiExitAltBuffer = "\x1b[?1049l\x1b[?25h";

    private const int MinRequiredWidth = 100;
    private const int MinRequiredHeight = 16;
    private const int DefaultFallbackWidth = 102;
    private const int DefaultFallbackHeight = 30;

    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly List<NetworkAuditEntry> _recentDiscoveries = [];
    private readonly object _syncLock = new();
    private readonly int _scanIntervalSeconds;
    private readonly string _outputCsvPath;

    private FlowAuditCycleResult? _lastResult;
    private ServerHostMetadata? _lastHost;
    private int _lastWindowWidth = -1;
    private int _lastWindowHeight = -1;
    private int _lastWindowTop = -1;
    private int _lastWindowLeft = -1;

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
                // Active le Screen Buffer Alternatif ANSI (supprime l'ascenseur/historique) et masque le curseur
                Console.Write(AnsiEnterAltBuffer);
                Console.CursorVisible = false;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Environnement sans contrôle de terminal
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
    /// Restaure la visibilité du curseur et le tampon d'écran standard à la fermeture.
    /// </summary>
    public void RestoreCursor()
    {
        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Write(AnsiExitAltBuffer);
                Console.CursorVisible = true;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Toléré lors de la fermeture
        }
    }

    /// <summary>
    /// Met à jour les métadonnées et affiche la trame courante du tableau de bord.
    /// </summary>
    /// <param name="result">Synthèse métrique du dernier scan.</param>
    /// <param name="host">Métadonnées de la machine hôte.</param>
    public void Render(FlowAuditCycleResult result, ServerHostMetadata host)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(host);

        lock (_syncLock)
        {
            _lastResult = result;
            _lastHost = host;

            if (result.NewlyDiscoveredEntries.Count > 0)
            {
                foreach (var entry in result.NewlyDiscoveredEntries)
                {
                    _recentDiscoveries.Insert(0, entry);
                }

                while (_recentDiscoveries.Count > 100)
                {
                    _recentDiscoveries.RemoveAt(_recentDiscoveries.Count - 1);
                }
            }

            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Vérifie si la fenêtre a été redimensionnée ou si l'ascenseur a bougé, et redessine immédiatement le tableau de bord.
    /// </summary>
    public void CheckAndRedrawIfWindowChanged()
    {
        if (Console.IsOutputRedirected || _lastResult is null || _lastHost is null)
        {
            return;
        }

        try
        {
            var width = Console.WindowWidth;
            var height = Console.WindowHeight;
            var top = Console.WindowTop;
            var left = Console.WindowLeft;

            var changed = _lastWindowWidth != -1 && (
                width != _lastWindowWidth ||
                height != _lastWindowHeight ||
                top != _lastWindowTop ||
                left != _lastWindowLeft);

            if (changed)
            {
                lock (_syncLock)
                {
                    _lastWindowWidth = width;
                    _lastWindowHeight = height;
                    _lastWindowTop = top;
                    _lastWindowLeft = left;

                    RenderInternal(forceClear: true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Toléré si le terminal ne supporte pas l'interrogation de géométrie
        }
    }

    /// <summary>
    /// Force le réaffichage complet du tableau de bord après avoir effacé l'écran.
    /// </summary>
    public void ForceRedraw()
    {
        lock (_syncLock)
        {
            if (_lastResult is not null && _lastHost is not null)
            {
                RenderInternal(forceClear: true);
            }
        }
    }

    private void RenderInternal(bool forceClear)
    {
        if (forceClear)
        {
            ClearScreen();
        }

        if (!Console.IsOutputRedirected)
        {
            try
            {
                _lastWindowWidth = Console.WindowWidth;
                _lastWindowHeight = Console.WindowHeight;
                _lastWindowTop = Console.WindowTop;
                _lastWindowLeft = Console.WindowLeft;

                if (OperatingSystem.IsWindows() && (Console.WindowTop != 0 || Console.WindowLeft != 0))
                {
                    Console.SetWindowPosition(0, 0);
                    _lastWindowTop = 0;
                    _lastWindowLeft = 0;
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or PlatformNotSupportedException or ArgumentOutOfRangeException)
            {
                // Toléré selon capacités du terminal
            }
        }

        if (_lastResult is null || _lastHost is null)
        {
            return;
        }

        var currentWidth = _lastWindowWidth > 0 ? _lastWindowWidth : DefaultFallbackWidth;
        var currentHeight = _lastWindowHeight > 0 ? _lastWindowHeight : DefaultFallbackHeight;

        string frame;
        if (!Console.IsOutputRedirected && (currentWidth < MinRequiredWidth || currentHeight < MinRequiredHeight))
        {
            frame = BuildWindowTooSmallFrame(currentWidth, currentHeight, MinRequiredWidth, MinRequiredHeight);
        }
        else
        {
            // Utilise la largeur de fenêtre visible (moins 1 pour éviter l'enroulement forcé sous Windows)
            var targetWidth = Console.IsOutputRedirected ? DefaultFallbackWidth : Math.Max(MinRequiredWidth, currentWidth - 1);
            var targetHeight = Console.IsOutputRedirected ? DefaultFallbackHeight : Math.Max(MinRequiredHeight, currentHeight);
            frame = BuildDashboardFrame(_lastResult, _lastHost, targetWidth, targetHeight);
        }

        try
        {
            Console.Write(AnsiHome + frame);
        }
        catch (IOException)
        {
            // Erreur console tolérée
        }
    }

    internal static string BuildWindowTooSmallFrame(int currentWidth, int currentHeight, int minWidth = MinRequiredWidth, int minHeight = MinRequiredHeight)
    {
        var boxWidth = Math.Clamp(currentWidth - 2, 28, 80);
        var sb = new StringBuilder(512);

        sb.AppendLine($"{AnsiCyan}┌{new string('─', boxWidth)}┐{AnsiReset}{AnsiClearLine}");
        var msg1 = PadRightVisible($" {AnsiYellow}{AnsiBold}Fenêtre trop petite : {currentWidth}x{currentHeight}{AnsiReset}", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg1}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        var msg2 = PadRightVisible($" Minimum requis : {minWidth} colonnes x {minHeight} lignes", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg2}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        var msg3 = PadRightVisible(" Veuillez agrandir la fenêtre du terminal...", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg3}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        sb.AppendLine($"{AnsiCyan}└{new string('─', boxWidth)}┘{AnsiReset}{AnsiClearLine}");
        sb.Append($" {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quitter{AnsiClearLine}");

        return sb.ToString();
    }

    internal string BuildDashboardFrame(FlowAuditCycleResult result, ServerHostMetadata host)
    {
        return BuildDashboardFrame(result, host, DefaultFallbackWidth, DefaultFallbackHeight);
    }

    internal string BuildDashboardFrame(FlowAuditCycleResult result, ServerHostMetadata host, int totalWidth, int totalHeight)
    {
        var sb = new StringBuilder(8192);

        var uptimeStr = FormatUptime(_uptime.Elapsed);
        var timestampStr = DateTime.Now.ToString("HH:mm:ss");

        var interiorWidth = totalWidth - 2;

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

        // 1. En-tête principal & Volet Métriques
        const int metricsColWidth = 30;
        var infoColWidth = interiorWidth - (metricsColWidth + 1); // 1 pour séparateur central

        var headerDashesCount = Math.Max(1, infoColWidth - 22);

        sb.AppendLine($"{AnsiCyan}┌─ {AnsiBold}{AnsiWhite}NetFlowAudit v1.0.0{AnsiReset}{AnsiCyan} {new string('─', headerDashesCount)}┬─ {AnsiBold}{AnsiWhite}MÉTRIQUES{AnsiReset}{AnsiCyan} {new string('─', metricsColWidth - 12)}┐{AnsiReset}{AnsiClearLine}");

        // Calcul des largeurs des 3 sous-colonnes dans le volet d'information gauche
        // Col1 (fixe: 21) | Col3 (fixe: 20) | Col2 (prend tout le reste disponible)
        const int subCol1W = 21;
        const int subCol3W = 20;
        var subCol2W = Math.Max(10, infoColWidth - (subCol1W + subCol3W + 6)); // 6 pour les deux ' │ '

        var srv1 = $" Serveur : {AnsiWhite}{Truncate(host.HostName, 10),-10}{AnsiReset}";
        var srv2 = $"OS : {AnsiDarkGray}{Truncate(host.OperatingSystemDescription, subCol2W - 5),-5}{AnsiReset}";
        var srv3 = $"Uptime : {AnsiYellow}{uptimeStr,7}{AnsiReset}";
        var srvPart = FormatThreeSubColumns(srv1, subCol1W, srv2, subCol2W, srv3, subCol3W);
        var sockPart = $" Sockets actives : {AnsiGreen}{result.TotalSocketsScanned,6}{AnsiReset}";
        sb.AppendLine(FormatSplitRow(srvPart, infoColWidth, sockPart, metricsColWidth));

        var ip1 = $" IP Hôte : {AnsiWhite}{Truncate(host.PrimaryIpAddress, 10),-10}{AnsiReset}";
        var ip2 = $"Scan : {AnsiYellow}{_scanIntervalSeconds}s{AnsiReset} (auto)";
        var ip3 = $"Heure  : {AnsiDarkGray}{timestampStr,8}{AnsiReset}";
        var ipPart = FormatThreeSubColumns(ip1, subCol1W, ip2, subCol2W, ip3, subCol3W);
        var flowsPart = $" Flux uniques    : {AnsiCyan}{result.TotalUniqueFlowsTracked,6}{AnsiReset}";
        sb.AppendLine(FormatSplitRow(ipPart, infoColWidth, flowsPart, metricsColWidth));

        var st1 = $" Statut  : {AnsiGreen}Actif{AnsiReset}";
        var st2 = "Filtre: Ports/Loopback";
        var st3 = $"Ignorés: {AnsiYellow}{result.ExcludedSocketsCount,5}{AnsiReset} flux";
        var stPart = FormatThreeSubColumns(st1, subCol1W, st2, subCol2W, st3, subCol3W);
        var newPart = $" Nouveaux flux   : {AnsiMagenta}+{result.NewlyDiscoveredCount,-5}{AnsiReset}";
        sb.AppendLine(FormatSplitRow(stPart, infoColWidth, newPart, metricsColWidth));

        // 2. Séparateur vers volets médians
        const int trafficColWidth = 48;
        var topProcColWidth = interiorWidth - (trafficColWidth + 1);
        var middleBridgeDashes = Math.Max(1, infoColWidth - trafficColWidth - 1);

        sb.AppendLine($"{AnsiCyan}├{new string('─', trafficColWidth)}┬{new string('─', middleBridgeDashes)}┴{new string('─', metricsColWidth)}┤{AnsiReset}{AnsiClearLine}");

        // 3. Volets médians (Trafic vs Top Processus)
        var titleTrafic = $" {AnsiBold}{AnsiWhite}RÉPARTITION DU TRAFIC RÉSEAU{AnsiReset}";
        var titleProc = $" {AnsiBold}{AnsiWhite}TOP PROCESSUS ACTIFS (SOCKETS){AnsiReset}";
        sb.AppendLine(FormatSplitRow(titleTrafic, trafficColWidth, titleProc, topProcColWidth));

        var tcpLine = $" TCP : {AnsiGreen}{tcpSockets,4}{AnsiReset} (Écoute: {listeningCount,3}, Sortant: {outboundCount,3})";
        var proc1 = FormatTopProcess(topProcesses, 0, 1, topProcColWidth);
        sb.AppendLine(FormatSplitRow(tcpLine, trafficColWidth, proc1, topProcColWidth));

        var udpLine = $" UDP : {AnsiYellow}{udpSockets,4}{AnsiReset} (Écoute: {udpSockets,3})";
        var proc2 = FormatTopProcess(topProcesses, 1, 2, topProcColWidth);
        sb.AppendLine(FormatSplitRow(udpLine, trafficColWidth, proc2, topProcColWidth));

        var inLine = $" Flux entrants établis : {AnsiCyan}{inboundCount,4}{AnsiReset}";
        var proc3 = FormatTopProcess(topProcesses, 2, 3, topProcColWidth);
        sb.AppendLine(FormatSplitRow(inLine, trafficColWidth, proc3, topProcColWidth));

        var emptyLine = string.Empty;
        var proc4 = FormatTopProcess(topProcesses, 3, 4, topProcColWidth);
        sb.AppendLine(FormatSplitRow(emptyLine, trafficColWidth, proc4, topProcColWidth));

        // 4. Séparateur vers tableau des découvertes
        sb.AppendLine($"{AnsiCyan}├{new string('─', trafficColWidth)}┴{new string('─', topProcColWidth)}┤{AnsiReset}{AnsiClearLine}");

        // 5. Titre et colonnes du tableau des découvertes
        var tableTitle = $" {AnsiBold}{AnsiWhite}DERNIÈRES DÉCOUVERTES DE FLUX (TEMPS RÉEL){AnsiReset}";
        sb.AppendLine(FormatRow(tableTitle, interiorWidth));

        // Calcul dynamique des largeurs des colonnes du tableau
        var extraWidth = Math.Max(0, interiorWidth - 100);
        var localWidth = Math.Clamp(21 + (extraWidth / 4), 21, 32);
        var remoteWidth = Math.Clamp(21 + (extraWidth / 4), 21, 32);
        var procWidth = Math.Clamp(12 + (extraWidth / 6), 12, 22);
        var targetWidth = Math.Max(15, interiorWidth - (21 + localWidth + 1 + remoteWidth + 1 + procWidth + 1));

        var headerColLocal = "LOCAL".PadRight(localWidth);
        var headerColRemote = "DISTANT".PadRight(remoteWidth);
        var headerColProc = "PROCESSUS".PadRight(procWidth);
        var tableHeader = $" {AnsiDarkGray}HEURE (UTC)  PROTO DIR  {headerColLocal} {headerColRemote} {headerColProc} CIBLE / CMD{AnsiReset}";
        sb.AppendLine(FormatRow(tableHeader, interiorWidth));

        // Calcul du nombre de lignes disponibles pour les découvertes selon la hauteur de la fenêtre
        // 15 lignes fixes au total hors découvertes
        var maxVisibleDiscoveries = Math.Max(3, totalHeight - 15);

        for (var i = 0; i < maxVisibleDiscoveries; i++)
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

                var local = Truncate($"{entry.Socket.LocalEndpoint.Address}:{entry.Socket.LocalEndpoint.Port}", localWidth);
                var remoteTarget = !string.IsNullOrWhiteSpace(entry.RemoteHostName)
                    ? $"{entry.RemoteHostName}:{entry.Socket.RemoteEndpoint.Port}"
                    : $"{entry.Socket.RemoteEndpoint.Address}:{entry.Socket.RemoteEndpoint.Port}";
                var remote = Truncate(remoteTarget, remoteWidth);
                var proc = Truncate(entry.Socket.Process.ProcessName, procWidth);
                var target = Truncate(!string.IsNullOrWhiteSpace(entry.Socket.Process.TargetExecutable)
                    ? entry.Socket.Process.TargetExecutable
                    : entry.Socket.Process.CommandLine, targetWidth);

                var color = entry.Socket.Protocol == SocketTransportProtocol.Tcp ? AnsiGreen : AnsiYellow;
                var row = $" {AnsiDarkGray}{time}{AnsiReset}  {color}{proto}{AnsiReset} {dir} {local.PadRight(localWidth)} {remote.PadRight(remoteWidth)} {AnsiMagenta}{proc.PadRight(procWidth)}{AnsiReset} {AnsiWhite}{target.PadRight(targetWidth)}{AnsiReset}";
                sb.AppendLine(FormatRow(row, interiorWidth));
            }
            else
            {
                sb.AppendLine(FormatRow(string.Empty, interiorWidth));
            }
        }

        // 6. Pied de page
        sb.AppendLine($"{AnsiCyan}└{new string('─', interiorWidth)}┘{AnsiReset}{AnsiClearLine}");
        sb.Append($" {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quitter  {AnsiBold}{AnsiYellow}[C]{AnsiReset} Effacer  {AnsiDarkGray}| Journal : {_outputCsvPath} (RFC 4180){AnsiReset}{AnsiClearLine}");

        return sb.ToString();
    }

    private static string FormatThreeSubColumns(string c1, int w1, string c2, int w2, string c3, int w3)
    {
        var p1 = PadRightVisible(c1, w1);
        var p2 = PadRightVisible(c2, w2);
        var p3 = PadRightVisible(c3, w3);
        return $"{p1} {AnsiDarkGray}│{AnsiReset} {p2} {AnsiDarkGray}│{AnsiReset} {p3}";
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

    private static string FormatTopProcess(List<KeyValuePair<string, int>> list, int index, int rank, int totalColWidth)
    {
        if (index >= list.Count)
        {
            return $" {rank}. ---";
        }

        var item = list[index];
        var maxNameLen = Math.Max(10, totalColWidth - 19);
        var name = Truncate(item.Key, maxNameLen);
        return $" {rank}. {name.PadRight(maxNameLen)} ({item.Value,3} sockets)";
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
        if (visibleLen > targetWidth)
        {
            return Truncate(text, targetWidth);
        }

        if (visibleLen == targetWidth)
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
