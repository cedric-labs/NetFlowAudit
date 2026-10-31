namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// Explanation of each accepted value of the choice options; the CLI help describes the option but not its values,
/// and a test keeps this table in step with the values the CLI accepts.
/// </summary>
public static class ChoiceValueDescriptions
{
    /// <summary>
    /// Descriptions keyed by option long name, then by value.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Entries { get; } =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["--zone"] = Values(
                ("all", "Every destination."),
                ("internal", "Destinations in the private address ranges only."),
                ("public", "Destinations outside the private address ranges only.")),
            ["--direction"] = Values(
                ("all", "Inbound and outbound flows."),
                ("in", "Inbound flows only."),
                ("out", "Outbound flows only.")),
            ["--layout"] = Values(
                ("layered", "Tiered layout, in columns or in concentric rings around the audited server."),
                ("tree", "Top-down tree: roots at the top, targets in compact columns below."),
                ("horizontal-tree", "Horizontal tree: roots on the left, flows running from left to right."),
                ("organic", "Force-directed layout: linked servers attract each other, the others repel."),
                ("circular", "Servers evenly spaced on a ring."),
                ("grid", "Multi-column grid.")),
            ["--sort"] = Values(
                ("barycentric", "Places interconnected servers face to face to reduce arrow crossings."),
                ("tiers", "By functional tier: clients, gateways, applications, then databases and infrastructure."),
                ("subnet", "Grouped by IP subnet and contiguous network security zones."),
                ("degree", "By traffic density: total inbound and outbound connections, highest first."),
                ("alpha", "Alphabetical by server host name."),
                ("ip", "By subnet and IP address octets.")),
            ["--routing"] = Values(
                ("bezier", "Curved arrows that bend around the servers in between."),
                ("orthogonal", "Right-angled arrows in separate lanes so that they do not overlap."),
                ("straight", "Straight segments between server borders, offset when several links join the same pair."),
                ("bus", "The arrows of a server share one trunk per direction, then split into one branch per target. Designed for the tree layout.")),
            ["--edge-labels"] = Values(
                ("ports", "Each flow keeps its own arrow, labelled with its port."),
                ("none", "The flows of a server pair are merged into one unlabelled arrow.")),
            ["--spacing"] = Values(
                ("compact", "Tight spacing between servers."),
                ("balanced", "Default spacing."),
                ("spacious", "Wide spacing between servers.")),
            ["--drawio-edges"] = Values(
                ("adaptive", "One edge per flow up to 3 ports, merged into a single trunk beyond that."),
                ("direction", "Flows sharing the same source, target and direction are merged into one edge."),
                ("pair", "Every flow of a server pair is merged into one edge, double-headed when flows go both ways."),
                ("none", "One edge per listening port, with anchors spread over the whole side.")),
            ["--drawio-subnets"] = Values(
                ("on", "Servers are grouped in subnet zones."),
                ("off", "Servers are laid out as a plain grid without zones.")),
            ["--drawio-theme"] = Values(
                ("dark", "Dark colour scheme."),
                ("light", "Light colour scheme."))
        };

    /// <summary>
    /// Returns the description of a value.
    /// </summary>
    /// <param name="optionName">Long name of the option.</param>
    /// <param name="value">Accepted value of the option.</param>
    /// <returns>The description, or an empty string when none is declared.</returns>
    public static string For(string optionName, string value) =>
        Entries.TryGetValue(optionName, out var values) && values.TryGetValue(value, out var description)
            ? description
            : string.Empty;

    private static Dictionary<string, string> Values(params (string Value, string Description)[] entries) =>
        entries.ToDictionary(entry => entry.Value, entry => entry.Description, StringComparer.Ordinal);
}
