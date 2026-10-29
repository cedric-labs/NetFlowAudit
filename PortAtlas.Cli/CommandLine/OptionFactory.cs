using System.CommandLine;
using System.Globalization;

namespace PortAtlas.Cli.CommandLine;

/// <summary>
/// Creates the option shapes shared by several commands, so that lists, bounded numbers and closed value sets
/// are parsed and validated the same way everywhere.
/// </summary>
internal static class OptionFactory
{
    private static readonly char[] ListSeparators = [','];

    /// <summary>
    /// Option accepting comma-separated values, repeatable: <c>--x a,b --x c</c> gives <c>a, b, c</c>.
    /// </summary>
    public static Option<string[]> CreateList(string name, string helpName, string description) =>
        new(name)
        {
            Description = description,
            HelpName = helpName,
            CustomParser = result => [.. result.Tokens.SelectMany(t => t.Value.Split(ListSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))]
        };

    /// <summary>
    /// Option accepting one value among <paramref name="acceptedValues"/>, compared without case and stored as written in
    /// <paramref name="acceptedValues"/>; any other value is a parse error listing them.
    /// </summary>
    public static Option<string?> CreateChoice(string name, string description, params string[] acceptedValues) =>
        new(name)
        {
            Description = description,
            HelpName = string.Join('|', acceptedValues),
            CustomParser = result =>
            {
                var given = result.Tokens.Count > 0 ? result.Tokens[0].Value : string.Empty;
                var accepted = Array.Find(acceptedValues, v => v.Equals(given, StringComparison.OrdinalIgnoreCase));
                if (accepted is null)
                {
                    result.AddError($"'{given}' is not accepted by {name}. Accepted values: {string.Join(", ", acceptedValues)}.");
                }

                return accepted;
            }
        };

    /// <summary>
    /// Integer option rejecting values lower than <paramref name="minimum"/>.
    /// </summary>
    public static Option<int?> CreateInteger(string name, string helpName, string description, int minimum)
    {
        var option = new Option<int?>(name) { Description = description, HelpName = helpName };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() < minimum)
            {
                result.AddError(string.Create(CultureInfo.InvariantCulture, $"{name} must be greater than or equal to {minimum}."));
            }
        });
        return option;
    }

    /// <summary>
    /// <c>--dns-timeout-ms</c>, shared by the commands that send reverse DNS queries (configuration key <c>DnsResolutionTimeoutMs</c>).
    /// </summary>
    public static Option<int?> CreateDnsTimeout() =>
        CreateInteger("--dns-timeout-ms", "milliseconds", "Time to wait for a reverse DNS answer. Default: 3000.", minimum: 1);

    /// <summary>
    /// Option holding a single file path.
    /// </summary>
    public static Option<string?> CreatePath(string name, string description, params string[] aliases) =>
        new(name, aliases) { Description = description, HelpName = "path" };

    /// <summary>
    /// Returns the list given on the command line, or <paramref name="fallback"/> when the option is absent.
    /// </summary>
    public static List<string> ReadList(ParseResult parseResult, Option<string[]> option, List<string> fallback) =>
        parseResult.GetResult(option) is null ? fallback : [.. parseResult.GetValue(option) ?? []];
}
