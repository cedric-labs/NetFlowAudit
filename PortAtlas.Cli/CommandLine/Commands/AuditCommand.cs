using System.CommandLine;
using System.Globalization;
using System.Net;
using PortAtlas.Cli.Audit;
using PortAtlas.Cli.CommandLine.OptionSets;
using PortAtlas.Cli.Configuration;

namespace PortAtlas.Cli.CommandLine.Commands;

/// <summary>
/// <c>portatlas audit</c>: continuous socket audit with the terminal dashboard, or without it for a service.
/// </summary>
public sealed class AuditCommand : Command
{
    private const string AllHistory = "all";
    private const string ObfuscatedServerName = "HOMELAB-01";

    // Display-only placeholder shown instead of the real local address in screenshots; it never reaches the network code.
    private static readonly string ObfuscatedLocalIp = new IPAddress([192, 168, 1, 10]).ToString();

    private readonly Option<string?> _output = OptionFactory.CreatePath("--output", "CSV audit log to write. Tokens {ServerName}, {HostName}, {Date}, {Timestamp} are expanded. Default: netflow_audit.csv.", "-o");
    private readonly Option<string?> _ndjson = OptionFactory.CreatePath("--ndjson", "NDJSON (JSON Lines) stream written alongside the CSV log. Tokens are expanded as for --output.");
    private readonly Option<int?> _interval = OptionFactory.CreateInteger("--interval", "seconds", "Seconds between two socket scans. Default: 5.", minimum: 1);
    private readonly Option<string[]> _columns = OptionFactory.CreateList("--columns", "names", "Ordered CSV columns to write. Default: all 20 columns.");
    private readonly Option<string?> _reloadHistory = new("--reload-history") { Description = "Flows reloaded from the existing CSV log at startup: all, or a number (0 disables the reload). Default: all.", HelpName = "all|count" };
    private readonly Option<bool> _noTui = new("--no-tui") { Description = "Run without the terminal dashboard (Windows service, systemd unit)." };
    private readonly Option<bool> _noDns = new("--no-dns") { Description = "Do not resolve remote addresses with reverse DNS." };
    private readonly Option<int?> _dnsTimeout = OptionFactory.CreateDnsTimeout();
    private readonly Option<bool> _noRdap = new("--no-rdap") { Description = "Do not query RDAP registries (Internet) for address owners; reverse DNS stays active." };
    private readonly Option<string[]> _excludeRemoteIps = OptionFactory.CreateList("--exclude-remote-ips", "addresses", "Remote IP addresses or CIDR subnets that are not recorded.");
    private readonly Option<string[]> _alias = OptionFactory.CreateList("--alias", "ip=host", "Host name used for an IP address instead of reverse DNS (ip=host or ip:host). Repeatable.");
    private readonly Option<string?> _maskServer = new("--mask-server") { Description = "Server name displayed by the dashboard instead of the real one (screenshots).", HelpName = "name" };
    private readonly Option<string?> _maskIp = new("--mask-ip") { Description = "Local IP address displayed by the dashboard instead of the real one (screenshots).", HelpName = "address" };
    private readonly Option<bool> _obfuscate = new("--obfuscate") { Description = "Display HOMELAB-01 and 192.168.1.10 unless --mask-server or --mask-ip is given." };
    private readonly FlowFilterOptionSet _filters = new();
    private readonly MapRenderingOptionSet _rendering = new();
    private readonly Option<string?> _configFile;

    /// <summary>
    /// Creates the command.
    /// </summary>
    /// <param name="configFile">Global <c>--config</c> option.</param>
    public AuditCommand(Option<string?> configFile)
        : base("audit", "Scan TCP/UDP sockets continuously and record deduplicated flows.")
    {
        _configFile = configFile;
        _reloadHistory.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string?>();
            if (value is not null && !TryParseReloadHistory(value, out _))
            {
                result.AddError("--reload-history accepts 'all' or a number greater than or equal to 0.");
            }
        });

        Options.Add(_output);
        Options.Add(_ndjson);
        Options.Add(_interval);
        Options.Add(_columns);
        Options.Add(_reloadHistory);
        Options.Add(_noTui);
        Options.Add(_noDns);
        Options.Add(_noRdap);
        Options.Add(_dnsTimeout);
        Options.Add(_excludeRemoteIps);
        Options.Add(_alias);
        Options.Add(_maskServer);
        Options.Add(_maskIp);
        Options.Add(_obfuscate);
        _filters.AddTo(this);
        _rendering.AddTo(this);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var loaded = AppConfigurationFile.Load(parseResult.GetValue(_configFile));
            var config = AppConfigurationFile.ResolveOutputPathTokens(BuildConfiguration(parseResult, loaded.Configuration));
            return await ContinuousAuditRunner.RunAsync(config, loaded.FilePath, cancellationToken).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// Overrides the configuration file values with the options given on the command line.
    /// </summary>
    /// <param name="parseResult">Parsed command line of this command.</param>
    /// <param name="fileConfiguration">Values read from the configuration file.</param>
    /// <returns>The configuration the audit runs with (output path tokens not yet expanded).</returns>
    /// <exception cref="ArgumentNullException">When an argument is null.</exception>
    public AppConfiguration BuildConfiguration(ParseResult parseResult, AppConfiguration fileConfiguration)
    {
        ArgumentNullException.ThrowIfNull(parseResult);
        ArgumentNullException.ThrowIfNull(fileConfiguration);

        var config = _rendering.Apply(parseResult, _filters.Apply(parseResult, fileConfiguration));

        var reloadHistoryLimit = config.ReloadHistoryLimit;
        if (parseResult.GetValue(_reloadHistory) is { } reloadHistory && TryParseReloadHistory(reloadHistory, out var parsedLimit))
        {
            reloadHistoryLimit = parsedLimit;
        }

        var obfuscate = parseResult.GetValue(_obfuscate);
        var maskServer = parseResult.GetValue(_maskServer) ?? (obfuscate ? ObfuscatedServerName : config.MaskServerName);
        var maskIp = parseResult.GetValue(_maskIp) ?? (obfuscate ? ObfuscatedLocalIp : config.MaskLocalIp);

        return config with
        {
            OutputCsvPath = parseResult.GetValue(_output) ?? config.OutputCsvPath,
            OutputNdjsonPath = parseResult.GetValue(_ndjson) ?? config.OutputNdjsonPath,
            ScanIntervalSeconds = parseResult.GetValue(_interval) ?? config.ScanIntervalSeconds,
            CsvColumns = OptionFactory.ReadList(parseResult, _columns, config.CsvColumns),
            ReloadHistoryLimit = reloadHistoryLimit,
            EnableDashboard = config.EnableDashboard && !parseResult.GetValue(_noTui),
            ResolveRemoteHostNames = config.ResolveRemoteHostNames && !parseResult.GetValue(_noDns),
            ResolveIpOwners = config.ResolveIpOwners && !parseResult.GetValue(_noRdap),
            DnsResolutionTimeoutMs = parseResult.GetValue(_dnsTimeout) ?? config.DnsResolutionTimeoutMs,
            ExcludedRemoteIps = OptionFactory.ReadList(parseResult, _excludeRemoteIps, config.ExcludedRemoteIps),
            CustomHostAliases = MergeAliases(config.CustomHostAliases, parseResult.GetValue(_alias) ?? []),
            MaskServerName = maskServer,
            MaskLocalIp = maskIp
        };
    }

    private static bool TryParseReloadHistory(string value, out int? limit)
    {
        if (value.Equals(AllHistory, StringComparison.OrdinalIgnoreCase))
        {
            limit = null;
            return true;
        }

        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var count))
        {
            limit = count;
            return true;
        }

        limit = null;
        return false;
    }

    private static Dictionary<string, string> MergeAliases(Dictionary<string, string> configured, IEnumerable<string> pairs)
    {
        var aliases = new Dictionary<string, string>(configured, StringComparer.OrdinalIgnoreCase);
        foreach (var pair in pairs)
        {
            var separatorIndex = pair.IndexOfAny(['=', ':']);
            if (separatorIndex <= 0 || separatorIndex >= pair.Length - 1)
            {
                continue;
            }

            var ip = pair[..separatorIndex].Trim();
            var host = pair[(separatorIndex + 1)..].Trim();
            if (ip.Length > 0 && host.Length > 0)
            {
                aliases[ip] = host;
            }
        }

        return aliases;
    }
}
