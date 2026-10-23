using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Core.Filtering;
using NetFlowAudit.Core.Models;
using ProcessForge.Filtering;
using SocketForge.Models;

namespace NetFlowAudit.Cli.Dashboard;

/// <summary>
/// Interactive terminal dashboard renderer inspired by btop for NetFlowAudit.
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
    private const string AnsiSelectedCell = "\x1b[30;43;1m";
    private const string AnsiHome = "\x1b[H";
    private const string AnsiClearLine = "\x1b[K";
    private const string AnsiEnterAltBuffer = "\x1b[?1049h\x1b[H\x1b[2J";
    private const string AnsiExitAltBuffer = "\x1b[?1049l\x1b[?25h";

    private const int MinRequiredWidth = 100;
    private const int MinRequiredHeight = 16;
    private const int DefaultFallbackWidth = 102;
    private const int DefaultFallbackHeight = 30;
    private const int UnlimitedDiscoveriesCapacity = 50_000;

    private readonly Stopwatch _uptime = Stopwatch.StartNew();
    private readonly List<NetworkAuditEntry> _recentDiscoveries = [];
    private readonly object _syncLock = new();
    private readonly int _scanIntervalSeconds;
    private readonly string _outputCsvPath;
    private readonly INetworkFlowFilter? _networkFlowFilter;
    private readonly ProcessExclusionFilter? _processExclusionFilter;
    private readonly IReadOnlySet<IPAddress>? _localHostAddresses;

    private FlowAuditCycleResult? _lastResult;
    private ServerHostMetadata? _lastHost;
    private int _lastWindowWidth = -1;
    private int _lastWindowHeight = -1;
    private int _lastWindowTop = -1;
    private int _lastWindowLeft = -1;

    private bool _isSelectionActive;
    private int _selectedRowIndex;
    private int _currentPageIndex;
    private int _maxStoredDiscoveries = 100;
    private InteractiveExclusionColumn _selectedColumn = InteractiveExclusionColumn.Local;
    private string? _statusBannerMessage;
    private DateTime _statusBannerExpiresUtc = DateTime.MinValue;

    /// <summary>
    /// Indicates whether interactive row/column selection mode is currently active.
    /// </summary>
    public bool IsSelectionActive
    {
        get
        {
            lock (_syncLock)
            {
                return _isSelectionActive;
            }
        }
    }

    /// <summary>
    /// Zero-based index of the currently selected row within the active page.
    /// </summary>
    public int SelectedRowIndex
    {
        get
        {
            lock (_syncLock)
            {
                return _selectedRowIndex;
            }
        }
    }

    /// <summary>
    /// Zero-based index of the currently displayed page.
    /// </summary>
    public int CurrentPageIndex
    {
        get
        {
            lock (_syncLock)
            {
                return _currentPageIndex;
            }
        }
    }

    /// <summary>
    /// Total number of pages available for the currently stored flow discoveries.
    /// </summary>
    public int TotalPages
    {
        get
        {
            lock (_syncLock)
            {
                var pageSize = GetVisiblePageSize();
                return Math.Max(1, (int)Math.Ceiling(_recentDiscoveries.Count / (double)pageSize));
            }
        }
    }

    /// <summary>
    /// Maximum number of flow discoveries kept in the paginated dashboard buffer (100 by default).
    /// </summary>
    public int MaxStoredDiscoveries
    {
        get
        {
            lock (_syncLock)
            {
                return _maxStoredDiscoveries;
            }
        }
    }

    /// <summary>
    /// Currently selected interactive exclusion column.
    /// </summary>
    public InteractiveExclusionColumn SelectedColumn
    {
        get
        {
            lock (_syncLock)
            {
                return _selectedColumn;
            }
        }
    }

    /// <summary>
    /// Initializes the terminal dashboard renderer.
    /// </summary>
    /// <param name="scanIntervalSeconds">Configured scan interval in seconds.</param>
    /// <param name="outputCsvPath">Output CSV file path.</param>
    /// <param name="networkFlowFilter">Optional network flow filter.</param>
    /// <param name="processExclusionFilter">Optional process exclusion filter.</param>
    /// <param name="localHostAddresses">Optional set of local host IP addresses.</param>
    public FlowAuditDashboardRenderer(
        int scanIntervalSeconds,
        string outputCsvPath,
        INetworkFlowFilter? networkFlowFilter,
        ProcessExclusionFilter? processExclusionFilter = null,
        IReadOnlySet<IPAddress>? localHostAddresses = null)
    {
        _scanIntervalSeconds = scanIntervalSeconds;
        _outputCsvPath = outputCsvPath;
        _networkFlowFilter = networkFlowFilter;
        _processExclusionFilter = processExclusionFilter;
        _localHostAddresses = localHostAddresses;

        try
        {
            if (!Console.IsOutputRedirected)
            {
                Console.Write(AnsiEnterAltBuffer);
                Console.CursorVisible = false;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Environment without terminal control
        }
    }

    /// <summary>
    /// Compatibility constructor without explicit exclusion filters.
    /// </summary>
    /// <param name="scanIntervalSeconds">Configured scan interval in seconds.</param>
    /// <param name="outputCsvPath">Output CSV file path.</param>
    public FlowAuditDashboardRenderer(int scanIntervalSeconds, string outputCsvPath)
        : this(scanIntervalSeconds, outputCsvPath, null, null, null)
    {
    }

    /// <summary>
    /// Clears the terminal screen.
    /// </summary>
    public void ClearScreen()
    {
        try
        {
            Console.Clear();
        }
        catch (IOException)
        {
            // Tolerated when no console is attached
        }
    }

    /// <summary>
    /// Restores cursor visibility and exits the alternate screen buffer on shutdown.
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
            // Tolerated during shutdown
        }
    }

    /// <summary>
    /// Moves the selection cursor one row up within the current visible page.
    /// </summary>
    public void MoveSelectionUp()
    {
        lock (_syncLock)
        {
            var visibleCount = GetVisibleCountOnCurrentPage();
            if (visibleCount == 0)
            {
                return;
            }

            if (!_isSelectionActive)
            {
                _isSelectionActive = true;
                _selectedRowIndex = 0;
            }
            else if (_selectedRowIndex > 0)
            {
                _selectedRowIndex--;
            }

            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Moves the selection cursor one row down, strictly stopping at the last visible row of the current page.
    /// </summary>
    public void MoveSelectionDown()
    {
        lock (_syncLock)
        {
            var visibleCount = GetVisibleCountOnCurrentPage();
            if (visibleCount == 0)
            {
                return;
            }

            if (!_isSelectionActive)
            {
                _isSelectionActive = true;
                _selectedRowIndex = 0;
            }
            else if (_selectedRowIndex < visibleCount - 1)
            {
                _selectedRowIndex++;
            }

            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Advances to the next page of flow discoveries if available.
    /// </summary>
    public void NextPage()
    {
        lock (_syncLock)
        {
            var pageSize = GetVisiblePageSize();
            var totalPages = Math.Max(1, (int)Math.Ceiling(_recentDiscoveries.Count / (double)pageSize));
            if (_currentPageIndex < totalPages - 1)
            {
                _currentPageIndex++;
                var visibleCount = GetVisibleCountOnCurrentPage();
                _selectedRowIndex = visibleCount > 0 ? Math.Clamp(_selectedRowIndex, 0, visibleCount - 1) : 0;
                RenderInternal(forceClear: false);
            }
        }
    }

    /// <summary>
    /// Returns to the previous page of flow discoveries if available.
    /// </summary>
    public void PreviousPage()
    {
        lock (_syncLock)
        {
            if (_currentPageIndex > 0)
            {
                _currentPageIndex--;
                var visibleCount = GetVisibleCountOnCurrentPage();
                _selectedRowIndex = visibleCount > 0 ? Math.Clamp(_selectedRowIndex, 0, visibleCount - 1) : 0;
                RenderInternal(forceClear: false);
            }
        }
    }

    /// <summary>
    /// Increases the number of flow discoveries retained in the paginated list by 100.
    /// </summary>
    /// <param name="batchSize">Batch size increment (100 by default).</param>
    public void LoadNextBatch(int batchSize = 100)
    {
        lock (_syncLock)
        {
            var increment = Math.Max(10, batchSize);
            _maxStoredDiscoveries = Math.Min(UnlimitedDiscoveriesCapacity, _maxStoredDiscoveries + increment);
            _statusBannerMessage = $"Buffer capacity increased to {_maxStoredDiscoveries} flows";
            _statusBannerExpiresUtc = DateTime.UtcNow.AddSeconds(3);
            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Expands the discovery buffer to retain all discovered flows across pages.
    /// </summary>
    public void ShowAllDiscoveries()
    {
        lock (_syncLock)
        {
            _maxStoredDiscoveries = UnlimitedDiscoveriesCapacity;
            _statusBannerMessage = "Buffer capacity set to ALL flows";
            _statusBannerExpiresUtc = DateTime.UtcNow.AddSeconds(3);
            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Moves the interactive column selection to the left.
    /// </summary>
    public void MoveSelectionLeft()
    {
        lock (_syncLock)
        {
            if (GetVisibleCountOnCurrentPage() == 0)
            {
                return;
            }

            if (!_isSelectionActive)
            {
                _isSelectionActive = true;
                _selectedRowIndex = 0;
                _selectedColumn = InteractiveExclusionColumn.Local;
            }
            else
            {
                _selectedColumn = _selectedColumn > InteractiveExclusionColumn.Local
                    ? _selectedColumn - 1
                    : InteractiveExclusionColumn.Target;
            }

            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Moves the interactive column selection to the right.
    /// </summary>
    public void MoveSelectionRight()
    {
        lock (_syncLock)
        {
            if (GetVisibleCountOnCurrentPage() == 0)
            {
                return;
            }

            if (!_isSelectionActive)
            {
                _isSelectionActive = true;
                _selectedRowIndex = 0;
                _selectedColumn = InteractiveExclusionColumn.Local;
            }
            else
            {
                _selectedColumn = _selectedColumn < InteractiveExclusionColumn.Target
                    ? _selectedColumn + 1
                    : InteractiveExclusionColumn.Local;
            }

            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Cancels interactive selection mode.
    /// </summary>
    public void CancelSelection()
    {
        lock (_syncLock)
        {
            _isSelectionActive = false;
            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Applies the exclusion rule matching the currently selected cell and updates the display.
    /// </summary>
    /// <param name="configurator">Flow exclusion configurator.</param>
    /// <returns>True if the exclusion was applied; otherwise false.</returns>
    public bool ApplyExclusionAtSelection(IFlowExclusionConfigurator configurator)
    {
        ArgumentNullException.ThrowIfNull(configurator);

        lock (_syncLock)
        {
            if (!_isSelectionActive || _recentDiscoveries.Count == 0)
            {
                return false;
            }

            var pageSize = GetVisiblePageSize();
            var globalIndex = (_currentPageIndex * pageSize) + _selectedRowIndex;
            if (globalIndex < 0 || globalIndex >= _recentDiscoveries.Count)
            {
                return false;
            }

            var entry = _recentDiscoveries[globalIndex];
            string actionDescription;

            switch (_selectedColumn)
            {
                case InteractiveExclusionColumn.Local:
                    var localPort = entry.Socket.LocalEndpoint.Port;
                    actionDescription = configurator.ExcludeLocalPort(localPort);
                    _recentDiscoveries.RemoveAll(e => e.Socket.LocalEndpoint.Port == localPort || IsEntryExcluded(e));
                    break;

                case InteractiveExclusionColumn.Remote:
                    var remoteAddress = entry.Socket.RemoteEndpoint.Address;
                    var normalizedIp = (remoteAddress.IsIPv4MappedToIPv6 ? remoteAddress.MapToIPv4() : remoteAddress).ToString();
                    actionDescription = configurator.ExcludeRemoteIp(normalizedIp);
                    _recentDiscoveries.RemoveAll(e => AddressesEqual(e.Socket.RemoteEndpoint.Address, remoteAddress) || IsEntryExcluded(e));
                    break;

                case InteractiveExclusionColumn.Process:
                    var procName = entry.Socket.Process.ProcessName;
                    actionDescription = configurator.ExcludeProcess(procName);
                    _recentDiscoveries.RemoveAll(e => string.Equals(e.Socket.Process.ProcessName, procName, StringComparison.OrdinalIgnoreCase) || IsEntryExcluded(e));
                    break;

                case InteractiveExclusionColumn.Target:
                    var targetName = !string.IsNullOrWhiteSpace(entry.Socket.Process.TargetExecutable)
                        ? entry.Socket.Process.TargetExecutable
                        : entry.Socket.Process.ProcessName;
                    actionDescription = configurator.ExcludeTarget(targetName);
                    _recentDiscoveries.RemoveAll(e =>
                        string.Equals(e.Socket.Process.TargetExecutable, targetName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Socket.Process.ProcessName, targetName, StringComparison.OrdinalIgnoreCase) ||
                        IsEntryExcluded(e));
                    break;

                default:
                    return false;
            }

            _statusBannerMessage = actionDescription;
            _statusBannerExpiresUtc = DateTime.UtcNow.AddSeconds(4);

            var totalPages = Math.Max(1, (int)Math.Ceiling(_recentDiscoveries.Count / (double)pageSize));
            if (_currentPageIndex >= totalPages)
            {
                _currentPageIndex = totalPages - 1;
            }

            var visibleCount = GetVisibleCountOnCurrentPage();
            if (visibleCount == 0)
            {
                _isSelectionActive = false;
                _selectedRowIndex = 0;
            }
            else
            {
                _selectedRowIndex = Math.Clamp(_selectedRowIndex, 0, visibleCount - 1);
            }

            RenderInternal(forceClear: false);
            return true;
        }
    }

    /// <summary>
    /// Updates cycle metrics and renders the current dashboard frame.
    /// </summary>
    /// <param name="result">Scan cycle summary metrics.</param>
    /// <param name="host">Local host metadata.</param>
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
                var newCount = 0;
                foreach (var entry in result.NewlyDiscoveredEntries)
                {
                    if (IsEntryExcluded(entry))
                    {
                        continue;
                    }

                    _recentDiscoveries.Insert(0, entry);
                    newCount++;
                }

                while (_recentDiscoveries.Count > _maxStoredDiscoveries)
                {
                    _recentDiscoveries.RemoveAt(_recentDiscoveries.Count - 1);
                }

                if (_isSelectionActive)
                {
                    var visibleCount = GetVisibleCountOnCurrentPage();
                    _selectedRowIndex = visibleCount > 0
                        ? Math.Clamp(_selectedRowIndex + newCount, 0, visibleCount - 1)
                        : 0;
                }
            }

            RenderInternal(forceClear: false);
        }
    }

    /// <summary>
    /// Updates the resolved remote hostname in the recent discoveries buffer when the IP matches.
    /// </summary>
    /// <param name="remoteAddress">Resolved remote IP address.</param>
    /// <param name="hostName">Resolved hostname.</param>
    public void UpdateRemoteHostName(IPAddress remoteAddress, string hostName)
    {
        ArgumentNullException.ThrowIfNull(remoteAddress);
        ArgumentNullException.ThrowIfNull(hostName);

        lock (_syncLock)
        {
            var updated = false;
            for (var i = 0; i < _recentDiscoveries.Count; i++)
            {
                var entry = _recentDiscoveries[i];
                if (entry.Socket.RemoteEndpoint.Address.Equals(remoteAddress) && string.IsNullOrWhiteSpace(entry.RemoteHostName))
                {
                    _recentDiscoveries[i] = entry with { RemoteHostName = hostName };
                    updated = true;
                }
            }

            if (updated && _lastResult is not null && _lastHost is not null)
            {
                RenderInternal(forceClear: false);
            }
        }
    }

    /// <summary>
    /// Checks whether the terminal window was resized or scrolled and redraws immediately.
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

                    var visibleCount = GetVisibleCountOnCurrentPage();
                    if (_isSelectionActive && visibleCount > 0)
                    {
                        _selectedRowIndex = Math.Clamp(_selectedRowIndex, 0, visibleCount - 1);
                    }

                    RenderInternal(forceClear: true);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Tolerated if terminal does not support geometry queries
        }
    }

    /// <summary>
    /// Forces a full screen clear and redraw of the dashboard.
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

    private int GetVisiblePageSize()
    {
        var currentHeight = _lastWindowHeight > 0 ? _lastWindowHeight : DefaultFallbackHeight;
        var targetHeight = Console.IsOutputRedirected ? DefaultFallbackHeight : Math.Max(MinRequiredHeight, currentHeight);
        return Math.Max(3, targetHeight - 15);
    }

    private int GetVisibleCountOnCurrentPage()
    {
        var pageSize = GetVisiblePageSize();
        var startIndex = _currentPageIndex * pageSize;
        if (startIndex >= _recentDiscoveries.Count)
        {
            return 0;
        }

        return Math.Min(pageSize, _recentDiscoveries.Count - startIndex);
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
                // Tolerated depending on terminal capabilities
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
            // Tolerated console I/O error
        }
    }

    internal static string BuildWindowTooSmallFrame(int currentWidth, int currentHeight, int minWidth = MinRequiredWidth, int minHeight = MinRequiredHeight)
    {
        var boxWidth = Math.Clamp(currentWidth - 2, 28, 80);
        var sb = new StringBuilder(512);

        sb.AppendLine($"{AnsiCyan}┌{new string('─', boxWidth)}┐{AnsiReset}{AnsiClearLine}");
        var msg1 = PadRightVisible($" {AnsiYellow}{AnsiBold}Window too small: {currentWidth}x{currentHeight}{AnsiReset}", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg1}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        var msg2 = PadRightVisible($" Minimum required: {minWidth} columns x {minHeight} rows", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg2}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        var msg3 = PadRightVisible(" Please enlarge your terminal window...", boxWidth);
        sb.AppendLine($"{AnsiCyan}│{AnsiReset}{msg3}{AnsiCyan}│{AnsiReset}{AnsiClearLine}");
        sb.AppendLine($"{AnsiCyan}└{new string('─', boxWidth)}┘{AnsiReset}{AnsiClearLine}");
        sb.Append($" {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quit{AnsiClearLine}");

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
            if (_processExclusionFilter is not null && _processExclusionFilter.ShouldExclude(s.Process))
            {
                continue;
            }

            if (_networkFlowFilter is not null && _networkFlowFilter.ShouldExclude(s, _localHostAddresses ?? new HashSet<IPAddress>()))
            {
                continue;
            }

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

            var procName = string.IsNullOrWhiteSpace(s.Process.ProcessName) ? "Unknown" : s.Process.ProcessName;
            processCounts[procName] = processCounts.GetValueOrDefault(procName) + 1;
        }

        var topProcesses = processCounts
            .OrderByDescending(kv => kv.Value)
            .Take(4)
            .ToList();

        // 1. Main Header & Metrics Pane
        const int metricsColWidth = 30;
        var infoColWidth = interiorWidth - (metricsColWidth + 1);

        var headerDashesCount = Math.Max(1, infoColWidth - 22);

        sb.AppendLine($"{AnsiCyan}┌─ {AnsiBold}{AnsiWhite}NetFlowAudit v1.0.0{AnsiReset}{AnsiCyan} {new string('─', headerDashesCount)}┬─ {AnsiBold}{AnsiWhite}METRICS{AnsiReset}{AnsiCyan} {new string('─', metricsColWidth - 10)}┐{AnsiReset}{AnsiClearLine}");

        const int subCol1W = 21;
        const int subCol3W = 20;
        var subCol2W = Math.Max(10, infoColWidth - (subCol1W + subCol3W + 6));

        var srv1 = $" Server  : {AnsiWhite}{Truncate(host.HostName, 10),-10}{AnsiReset}";
        var srv2 = $"OS : {AnsiDarkGray}{Truncate(host.OperatingSystemDescription, subCol2W - 5),-5}{AnsiReset}";
        var srv3 = $"Uptime : {AnsiYellow}{uptimeStr,7}{AnsiReset}";
        var srvPart = FormatThreeSubColumns(srv1, subCol1W, srv2, subCol2W, srv3, subCol3W);
        var sockPart = $" Active sockets  : {AnsiGreen}{result.TotalSocketsScanned,6}{AnsiReset}";
        sb.AppendLine(FormatSplitRow(srvPart, infoColWidth, sockPart, metricsColWidth));

        var ip1 = $" Host IP : {AnsiWhite}{Truncate(host.PrimaryIpAddress, 10),-10}{AnsiReset}";
        var ip2 = $"Scan : {AnsiYellow}{_scanIntervalSeconds}s{AnsiReset} (auto)";
        var ip3 = $"Time   : {AnsiDarkGray}{timestampStr,8}{AnsiReset}";
        var ipPart = FormatThreeSubColumns(ip1, subCol1W, ip2, subCol2W, ip3, subCol3W);
        var flowsPart = $" Unique flows    : {AnsiCyan}{result.TotalUniqueFlowsTracked,6}{AnsiReset}";
        sb.AppendLine(FormatSplitRow(ipPart, infoColWidth, flowsPart, metricsColWidth));

        var st1 = $" Status  : {AnsiGreen}Active{AnsiReset}";
        var st2 = "Filter: Ports/Loopback";
        var st3 = $"Ignored: {AnsiYellow}{result.ExcludedSocketsCount,5}{AnsiReset} flows";
        var stPart = FormatThreeSubColumns(st1, subCol1W, st2, subCol2W, st3, subCol3W);
        var newPart = $" New flows       : {AnsiMagenta}+{result.NewlyDiscoveredCount,-5}{AnsiReset}";
        sb.AppendLine(FormatSplitRow(stPart, infoColWidth, newPart, metricsColWidth));

        // 2. Separator to middle panes
        const int trafficColWidth = 48;
        var topProcColWidth = interiorWidth - (trafficColWidth + 1);
        var middleBridgeDashes = Math.Max(1, infoColWidth - trafficColWidth - 1);

        sb.AppendLine($"{AnsiCyan}├{new string('─', trafficColWidth)}┬{new string('─', middleBridgeDashes)}┴{new string('─', metricsColWidth)}┤{AnsiReset}{AnsiClearLine}");

        // 3. Middle panes (Traffic Breakdown vs Top Active Processes)
        var titleTrafic = $" {AnsiBold}{AnsiWhite}NETWORK TRAFFIC BREAKDOWN{AnsiReset}";
        var titleProc = $" {AnsiBold}{AnsiWhite}TOP ACTIVE PROCESSES (SOCKETS){AnsiReset}";
        sb.AppendLine(FormatSplitRow(titleTrafic, trafficColWidth, titleProc, topProcColWidth));

        var tcpLine = $" TCP : {AnsiGreen}{tcpSockets,4}{AnsiReset} (Listen: {listeningCount,3}, Outbound: {outboundCount,3})";
        var totalActiveSockets = result.TotalSocketsScanned;
        var proc1 = FormatTopProcess(topProcesses, 0, 1, topProcColWidth, totalActiveSockets);
        sb.AppendLine(FormatSplitRow(tcpLine, trafficColWidth, proc1, topProcColWidth));

        var udpLine = $" UDP : {AnsiYellow}{udpSockets,4}{AnsiReset} (Listen: {udpSockets,3})";
        var proc2 = FormatTopProcess(topProcesses, 1, 2, topProcColWidth, totalActiveSockets);
        sb.AppendLine(FormatSplitRow(udpLine, trafficColWidth, proc2, topProcColWidth));

        var inLine = $" Established inbound flows : {AnsiCyan}{inboundCount,4}{AnsiReset}";
        var proc3 = FormatTopProcess(topProcesses, 2, 3, topProcColWidth, totalActiveSockets);
        sb.AppendLine(FormatSplitRow(inLine, trafficColWidth, proc3, topProcColWidth));

        var emptyLine = string.Empty;
        var proc4 = FormatTopProcess(topProcesses, 3, 4, topProcColWidth, totalActiveSockets);
        sb.AppendLine(FormatSplitRow(emptyLine, trafficColWidth, proc4, topProcColWidth));

        // 4. Separator to discoveries table
        sb.AppendLine($"{AnsiCyan}├{new string('─', trafficColWidth)}┴{new string('─', topProcColWidth)}┤{AnsiReset}{AnsiClearLine}");

        // 5. Discoveries table title & pagination status
        var maxVisibleDiscoveries = Math.Max(3, totalHeight - 15);
        var totalPages = Math.Max(1, (int)Math.Ceiling(_recentDiscoveries.Count / (double)maxVisibleDiscoveries));
        if (_currentPageIndex >= totalPages)
        {
            _currentPageIndex = totalPages - 1;
        }

        var pageStartIndex = _currentPageIndex * maxVisibleDiscoveries;
        var visibleOnPage = Math.Clamp(_recentDiscoveries.Count - pageStartIndex, 0, maxVisibleDiscoveries);
        if (_isSelectionActive && visibleOnPage > 0)
        {
            _selectedRowIndex = Math.Clamp(_selectedRowIndex, 0, visibleOnPage - 1);
        }

        var startRowNum = _recentDiscoveries.Count == 0 ? 0 : pageStartIndex + 1;
        var endRowNum = pageStartIndex + visibleOnPage;
        var capLabel = _maxStoredDiscoveries >= UnlimitedDiscoveriesCapacity ? "ALL" : _maxStoredDiscoveries.ToString();

        var hasActiveBanner = !string.IsNullOrEmpty(_statusBannerMessage) && DateTime.UtcNow < _statusBannerExpiresUtc;
        var tableTitle = hasActiveBanner
            ? $" {AnsiBold}{AnsiGreen}✔ {_statusBannerMessage}{AnsiReset}"
            : $" {AnsiBold}{AnsiWhite}RECENT FLOW DISCOVERIES (LIVE){AnsiReset} {AnsiDarkGray}[Page {_currentPageIndex + 1}/{totalPages} | Rows {startRowNum}-{endRowNum} of {_recentDiscoveries.Count} | Max: {capLabel}]{AnsiReset}";
        sb.AppendLine(FormatRow(tableTitle, interiorWidth));

        // Fixed prefix width (21 chars): " " (1) + "TIME    " (8) + " " (1) + "PROTO" (5) + " " (1) + "DIR " (4) + " " (1) = 21 chars
        var extraWidth = Math.Max(0, interiorWidth - 100);
        var localWidth = Math.Clamp(21 + (extraWidth / 4), 21, 32);
        var remoteWidth = Math.Clamp(21 + (extraWidth / 4), 21, 32);
        var procWidth = Math.Clamp(12 + (extraWidth / 6), 12, 22);
        var targetWidth = Math.Max(15, interiorWidth - (21 + localWidth + 1 + remoteWidth + 1 + procWidth + 1));

        var rawLocalHeader = _isSelectionActive && _selectedColumn == InteractiveExclusionColumn.Local ? $"{AnsiYellow}{AnsiBold}[LOCAL]{AnsiReset}" : "LOCAL";
        var headerColLocal = PadRightVisible(rawLocalHeader, localWidth);

        var rawRemoteHeader = _isSelectionActive && _selectedColumn == InteractiveExclusionColumn.Remote ? $"{AnsiYellow}{AnsiBold}[REMOTE]{AnsiReset}" : "REMOTE";
        var headerColRemote = PadRightVisible(rawRemoteHeader, remoteWidth);

        var rawProcHeader = _isSelectionActive && _selectedColumn == InteractiveExclusionColumn.Process ? $"{AnsiYellow}{AnsiBold}[PROCESS]{AnsiReset}" : "PROCESS";
        var headerColProc = PadRightVisible(rawProcHeader, procWidth);

        var rawTargetHeader = _isSelectionActive && _selectedColumn == InteractiveExclusionColumn.Target ? $"{AnsiYellow}{AnsiBold}[TARGET / CMD]{AnsiReset}" : "TARGET / CMD";
        var headerColTarget = PadRightVisible(rawTargetHeader, targetWidth);

        var headerPrefix = $" {AnsiDarkGray}{"TIME",-8} {"PROTO",-5} {"DIR",-4} ";
        var tableHeader = $"{headerPrefix}{headerColLocal} {headerColRemote} {headerColProc} {headerColTarget}{AnsiReset}";
        sb.AppendLine(FormatRow(tableHeader, interiorWidth));

        for (var i = 0; i < maxVisibleDiscoveries; i++)
        {
            var dataIdx = pageStartIndex + i;
            if (dataIdx < _recentDiscoveries.Count)
            {
                var entry = _recentDiscoveries[dataIdx];
                var isRowSelected = _isSelectionActive && i == _selectedRowIndex;

                var time = entry.FirstSeenUtc.ToString("HH:mm:ss");
                var proto = entry.Socket.Protocol == SocketTransportProtocol.Tcp ? "TCP" : "UDP";
                var dir = entry.Socket.Direction switch
                {
                    SocketFlowDirection.Listening => "LIST",
                    SocketFlowDirection.Inbound => "IN",
                    SocketFlowDirection.Outbound => "OUT",
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

                var localText = local.PadRight(localWidth);
                if (isRowSelected && _selectedColumn == InteractiveExclusionColumn.Local)
                {
                    localText = $"{AnsiSelectedCell}{localText}{AnsiReset}";
                }

                var remoteText = remote.PadRight(remoteWidth);
                if (isRowSelected && _selectedColumn == InteractiveExclusionColumn.Remote)
                {
                    remoteText = $"{AnsiSelectedCell}{remoteText}{AnsiReset}";
                }

                var procText = proc.PadRight(procWidth);
                if (isRowSelected && _selectedColumn == InteractiveExclusionColumn.Process)
                {
                    procText = $"{AnsiSelectedCell}{procText}{AnsiReset}";
                }
                else
                {
                    procText = $"{AnsiMagenta}{procText}{AnsiReset}";
                }

                var targetText = target.PadRight(targetWidth);
                if (isRowSelected && _selectedColumn == InteractiveExclusionColumn.Target)
                {
                    targetText = $"{AnsiSelectedCell}{targetText}{AnsiReset}";
                }
                else
                {
                    targetText = $"{AnsiWhite}{targetText}{AnsiReset}";
                }

                var color = entry.Socket.Protocol == SocketTransportProtocol.Tcp ? AnsiGreen : AnsiYellow;
                var timeColor = isRowSelected ? $"{AnsiYellow}{AnsiBold}" : AnsiDarkGray;
                var rowPrefix = $" {timeColor}{time,-8}{AnsiReset} {color}{proto,-5}{AnsiReset} {dir,-4} ";
                var row = $"{rowPrefix}{localText} {remoteText} {procText} {targetText}";
                sb.AppendLine(FormatRow(row, interiorWidth));
            }
            else
            {
                sb.AppendLine(FormatRow(string.Empty, interiorWidth));
            }
        }

        // 6. Footer bar
        sb.AppendLine($"{AnsiCyan}└{new string('─', interiorWidth)}┘{AnsiReset}{AnsiClearLine}");

        if (_isSelectionActive)
        {
            var colLabel = _selectedColumn switch
            {
                InteractiveExclusionColumn.Local => "LOCAL",
                InteractiveExclusionColumn.Remote => "REMOTE",
                InteractiveExclusionColumn.Process => "PROCESS",
                InteractiveExclusionColumn.Target => "TARGET",
                _ => "FLOW"
            };

            sb.Append($" {AnsiBold}{AnsiYellow}[↑↓←→]{AnsiReset} Navigate  {AnsiBold}{AnsiYellow}[Enter/X]{AnsiReset} Exclude {colLabel}  {AnsiBold}{AnsiYellow}[N/P]{AnsiReset} Page  {AnsiBold}{AnsiYellow}[Esc]{AnsiReset} Cancel  {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quit{AnsiClearLine}");
        }
        else
        {
            sb.Append($" {AnsiBold}{AnsiYellow}[Q]{AnsiReset} Quit  {AnsiBold}{AnsiYellow}[C]{AnsiReset} Clear  {AnsiBold}{AnsiYellow}[M]{AnsiReset} Refresh Map  {AnsiBold}{AnsiYellow}[N/P]{AnsiReset} Next/Prev Page  {AnsiBold}{AnsiYellow}[L/A]{AnsiReset} +100/All  {AnsiBold}{AnsiYellow}[↑↓]{AnsiReset} Exclude{AnsiClearLine}");
        }

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

    private static string FormatTopProcess(List<KeyValuePair<string, int>> list, int index, int rank, int totalColWidth, int totalActiveSockets)
    {
        if (index >= list.Count)
        {
            return $" {rank}. ---";
        }

        var item = list[index];
        const int minWidthForBar = 28;
        if (totalColWidth < minWidthForBar)
        {
            var maxNameOnlyLen = Math.Max(6, totalColWidth - 16);
            var nameOnly = Truncate(item.Key, maxNameOnlyLen);
            return $" {rank}. {AnsiMagenta}{nameOnly.PadRight(maxNameOnlyLen)}{AnsiReset} ({item.Value,3} sockets)";
        }

        var remainingWidth = totalColWidth - 4 - 14;
        var procNameWidth = Math.Clamp(remainingWidth / 3, 8, 16);
        var barWidth = Math.Clamp(remainingWidth - procNameWidth - 3, 6, 24);
        var truncatedName = Truncate(item.Key, procNameWidth).PadRight(procNameWidth);

        var ratio = totalActiveSockets > 0
            ? Math.Clamp((double)item.Value / totalActiveSockets, 0.0, 1.0)
            : 0.0;
        var filledCount = (int)Math.Round(ratio * barWidth);
        if (item.Value > 0 && filledCount == 0)
        {
            filledCount = 1;
        }
        filledCount = Math.Clamp(filledCount, 0, barWidth);
        var emptyCount = barWidth - filledCount;

        var filledBlocks = new string('█', filledCount);
        var emptyBlocks = new string('░', emptyCount);

        var barPart = $"{AnsiCyan}[{filledBlocks}{AnsiDarkGray}{emptyBlocks}{AnsiCyan}]{AnsiReset}";
        return $" {rank}. {AnsiMagenta}{truncatedName}{AnsiReset} {barPart}  {AnsiWhite}{item.Value,3}{AnsiReset} sockets";
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

    private static bool AddressesEqual(IPAddress a, IPAddress b)
    {
        if (a.Equals(b))
        {
            return true;
        }

        var normA = a.IsIPv4MappedToIPv6 ? a.MapToIPv4() : a;
        var normB = b.IsIPv4MappedToIPv6 ? b.MapToIPv4() : b;
        return normA.Equals(normB);
    }

    private bool IsEntryExcluded(NetworkAuditEntry entry)
    {
        if (_processExclusionFilter is not null && _processExclusionFilter.ShouldExclude(entry.Socket.Process))
        {
            return true;
        }

        if (_networkFlowFilter is not null && _networkFlowFilter.ShouldExclude(entry.Socket, _localHostAddresses ?? new HashSet<IPAddress>()))
        {
            return true;
        }

        return false;
    }
}
