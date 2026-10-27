using System.CommandLine;
using NetFlowAudit.Core.Export;

namespace NetFlowAudit.Cli.CommandLine.OptionSets;

/// <summary>
/// <c>-i/--input</c>: audit logs read by the <c>map</c>, <c>export</c> and <c>resolve</c> commands.
/// </summary>
internal sealed class AuditInputOptionSet
{
    private readonly Option<string[]> _input = new("--input", "-i")
    {
        Description = "Audit log to read: a file, a directory (every audit file it contains) or a wildcard pattern such as audits/srv-*.csv. Repeatable. Default: the audit output file of the configuration.",
        HelpName = "path"
    };

    public void AddTo(Command command) => command.Options.Add(_input);

    /// <summary>
    /// Expands the inputs into existing files.
    /// </summary>
    /// <param name="parseResult">Parsed command line.</param>
    /// <param name="defaultInput">Input used when <c>--input</c> is absent.</param>
    /// <param name="directoryExtensions">Extensions read when an input is a directory.</param>
    public IReadOnlyList<string> ResolveFiles(ParseResult parseResult, string defaultInput, IReadOnlyCollection<string> directoryExtensions)
    {
        var requested = parseResult.GetValue(_input) is { Length: > 0 } given ? given : [defaultInput];
        return AuditInputPathResolver.Resolve(requested, directoryExtensions);
    }

    /// <summary>
    /// Describes the inputs for the "no audit file found" error message.
    /// </summary>
    public string Describe(ParseResult parseResult, string defaultInput) =>
        parseResult.GetValue(_input) is { Length: > 0 } given ? string.Join(", ", given) : defaultInput;
}
