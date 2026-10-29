using System.CommandLine;
using System.Net;
using PortAtlas.Cli.Configuration;
using PortAtlas.Cli.Diagnostics;

namespace PortAtlas.Cli.CommandLine.Commands;

/// <summary>
/// <c>portatlas lookup &lt;ip&gt;</c>: step-by-step reverse DNS and RDAP diagnostic of one address.
/// </summary>
public sealed class LookupCommand : Command
{
    private readonly Argument<string> _address = new("ip") { Description = "IPv4 or IPv6 address to diagnose." };
    private readonly Option<int?> _dnsTimeout = OptionFactory.CreateDnsTimeout();
    private readonly Option<string?> _configFile;

    /// <summary>
    /// Creates the command.
    /// </summary>
    /// <param name="configFile">Global <c>--config</c> option.</param>
    public LookupCommand(Option<string?> configFile)
        : base("lookup", "Diagnose the reverse DNS and RDAP resolution of one IP address (cache, OS settings, PTR query, RDAP query).")
    {
        _configFile = configFile;
        _address.Validators.Add(result =>
        {
            if (!IPAddress.TryParse(result.GetValueOrDefault<string>(), out _))
            {
                result.AddError($"'{result.GetValueOrDefault<string>()}' is not an IPv4 or IPv6 address.");
            }
        });
        Arguments.Add(_address);
        Options.Add(_dnsTimeout);

        SetAction(async (parseResult, cancellationToken) =>
        {
            var fileConfiguration = AppConfigurationFile.Load(parseResult.GetValue(_configFile)).Configuration;
            var config = fileConfiguration with
            {
                DnsResolutionTimeoutMs = parseResult.GetValue(_dnsTimeout) ?? fileConfiguration.DnsResolutionTimeoutMs
            };
            var address = parseResult.GetValue(_address) ?? string.Empty;
            await new IpResolutionDiagnosticReporter().RunDiagnosticAsync(address, config, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }
}
