using System.CommandLine;
using System.Net;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Diagnostics;

namespace NetFlowAudit.Cli.CommandLine.Commands;

/// <summary>
/// <c>netflowaudit lookup &lt;ip&gt;</c>: step-by-step reverse DNS and RDAP diagnostic of one address.
/// </summary>
public sealed class LookupCommand : Command
{
    private readonly Argument<string> _address = new("ip") { Description = "IPv4 or IPv6 address to diagnose." };
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

        SetAction(async (parseResult, cancellationToken) =>
        {
            var config = AppConfigurationFile.Load(parseResult.GetValue(_configFile)).Configuration;
            var address = parseResult.GetValue(_address) ?? string.Empty;
            await new IpResolutionDiagnosticReporter().RunDiagnosticAsync(address, config, cancellationToken).ConfigureAwait(false);
            return 0;
        });
    }
}
