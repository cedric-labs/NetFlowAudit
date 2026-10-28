using System.CommandLine;
using NetFlowAudit.Cli.CommandLine.OptionSets;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Enrichment;
using NetFlowAudit.Core.Enrichment;
using NetFlowAudit.Core.Export;

namespace NetFlowAudit.Cli.CommandLine.Commands;

/// <summary>
/// <c>netflowaudit resolve</c>: DNS cache consolidation and host name enrichment of audit logs, typically on a connected
/// workstation for logs collected on isolated servers.
/// </summary>
public sealed class ResolveCommand : Command
{
    private readonly AuditInputOptionSet _inputs = new();
    private readonly Option<string[]> _cache = new("--cache")
    {
        Description = "DNS cache to merge: a file, a directory or a wildcard pattern. Repeatable. Default: the DNS cache of the configuration.",
        HelpName = "path"
    };
    private readonly Option<string?> _outputCache = OptionFactory.CreatePath("--output-cache", "Merged master cache to write. Default: the DNS cache of the configuration (netflow_dns_cache.json).");
    private readonly Option<bool> _inPlace = new("--in-place") { Description = "Overwrite the audit logs instead of writing *_enriched files." };
    private readonly Option<bool> _forceResolve = new("--force-resolve") { Description = "Replace host names already present in the audit logs." };
    private readonly Option<int?> _rdapDelayMs = OptionFactory.CreateInteger("--rdap-delay-ms", "milliseconds", "Delay between two RDAP queries. Default: 200.", minimum: 0);
    private readonly Option<int?> _dnsTimeout = OptionFactory.CreateDnsTimeout();
    private readonly Option<bool> _cacheOnly = new("--cache-only") { Description = "Use only the merged caches: no DNS or RDAP query is sent." };
    private readonly Option<string?> _configFile;

    /// <summary>
    /// Creates the command.
    /// </summary>
    /// <param name="configFile">Global <c>--config</c> option.</param>
    public ResolveCommand(Option<string?> configFile)
        : base("resolve", "Merge DNS caches, resolve missing host names and write enriched audit logs.")
    {
        _configFile = configFile;
        _inputs.AddTo(this);
        Options.Add(_cache);
        Options.Add(_outputCache);
        Options.Add(_inPlace);
        Options.Add(_forceResolve);
        Options.Add(_rdapDelayMs);
        Options.Add(_cacheOnly);
        Options.Add(_dnsTimeout);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var config = AppConfigurationFile.Load(parseResult.GetValue(_configFile)).Configuration;
            var auditFiles = _inputs.ResolveFiles(parseResult, config.OutputCsvPath, AuditInputPathResolver.AuditLogExtensions);
            if (auditFiles.Count == 0)
            {
                await Console.Error.WriteLineAsync($"No audit log found for: {_inputs.Describe(parseResult, config.OutputCsvPath)}").ConfigureAwait(false);
                return 1;
            }

            var caches = parseResult.GetValue(_cache) is { Length: > 0 } given ? given : [config.DnsCacheFilePath];
            var options = new OfflineAuditEnrichmentOptions
            {
                InPlace = parseResult.GetValue(_inPlace),
                ForceResolve = parseResult.GetValue(_forceResolve),
                Isolated = parseResult.GetValue(_cacheOnly),
                RdapThrottleDelay = TimeSpan.FromMilliseconds(parseResult.GetValue(_rdapDelayMs) ?? 200),
                OutputCachePath = OutputFilePathResolver.Resolve(parseResult.GetValue(_outputCache) ?? config.DnsCacheFilePath)
            };

            var dnsTimeout = TimeSpan.FromMilliseconds(parseResult.GetValue(_dnsTimeout) ?? config.DnsResolutionTimeoutMs);
            return await new OfflineResolutionCommandRunner(dnsResolutionTimeout: dnsTimeout).RunAsync(auditFiles, caches, options, cancellationToken).ConfigureAwait(false);
        });
    }
}
