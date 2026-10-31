namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// Presentation of every option of the catalog, keyed by long name; an architecture test keeps it in step with the CLI.
/// </summary>
public static class OptionPresentation
{
    private static readonly OptionPresentationEntry Filter = new(OptionGroup.Filters, PathKind.None);
    private static readonly OptionPresentationEntry Rendering = new(OptionGroup.MapRendering, PathKind.None);
    private static readonly OptionPresentationEntry Resolving = new(OptionGroup.Resolution, PathKind.None);
    private static readonly OptionPresentationEntry Input = new(OptionGroup.Inputs, PathKind.FileOrDirectoryToRead);
    private static readonly OptionPresentationEntry Output = new(OptionGroup.Outputs, PathKind.FileToWrite);

    /// <summary>
    /// Entries keyed by option long name.
    /// </summary>
    public static IReadOnlyDictionary<string, OptionPresentationEntry> Entries { get; } = new Dictionary<string, OptionPresentationEntry>(StringComparer.Ordinal)
    {
        ["--input"] = Input,
        ["--cache"] = Input,
        ["--config"] = new(OptionGroup.Advanced, PathKind.FileToRead),

        ["--include-ports"] = Filter,
        ["--exclude-ports"] = Filter,
        ["--include-hosts"] = Filter,
        ["--exclude-hosts"] = Filter,
        ["--include-ips"] = Filter,
        ["--exclude-ips"] = Filter,
        ["--include-processes"] = Filter,
        ["--exclude-processes"] = Filter,
        ["--direction"] = Filter,
        ["--listeners-only"] = Filter,
        ["--include-ephemeral-ports"] = Filter,
        ["--min-observations"] = Filter,
        ["--active-within-days"] = Filter,
        ["--zone"] = Filter,

        ["--layout"] = Rendering,
        ["--sort"] = Rendering,
        ["--routing"] = Rendering,
        ["--edge-labels"] = Rendering,
        ["--spacing"] = Rendering,
        ["--drawio-edges"] = Rendering,
        ["--drawio-subnets"] = Rendering,
        ["--drawio-theme"] = Rendering,

        ["--html"] = Output,
        ["--drawio"] = Output,
        ["--output"] = Output,
        ["--pairs-output"] = Output,
        ["--output-cache"] = Output,

        ["--in-place"] = Resolving,
        ["--force-resolve"] = Resolving,
        ["--cache-only"] = Resolving,
        ["--rdap-delay-ms"] = Resolving,
        ["--dns-timeout-ms"] = Resolving
    };

    /// <summary>
    /// Returns the presentation of an option.
    /// </summary>
    /// <param name="optionName">Long name of the option.</param>
    /// <returns>The entry of the option, or one in <see cref="OptionGroup.Advanced"/> without path when none is declared.</returns>
    public static OptionPresentationEntry For(string optionName) =>
        Entries.TryGetValue(optionName, out var entry) ? entry : new OptionPresentationEntry(OptionGroup.Advanced, PathKind.None);
}
