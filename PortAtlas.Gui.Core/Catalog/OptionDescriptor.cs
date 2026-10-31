namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// An option of a command, as declared by the command tree of the CLI.
/// </summary>
/// <param name="Name">Long name with its dashes, for example <c>--layout</c>; the key of the option everywhere in the GUI.</param>
/// <param name="Aliases">Other names of the option, for example <c>-i</c>.</param>
/// <param name="Description">Help text of the option.</param>
/// <param name="Kind">Shape of the value the option takes.</param>
/// <param name="AcceptedValues">Closed set of values for a <see cref="OptionValueKind.Choice"/> option, empty otherwise.</param>
/// <param name="IsRepeatable">Whether the option can be given several times.</param>
/// <param name="IsGlobal">Whether the option is declared on an ancestor command and applies to every command below it.</param>
/// <param name="IsRequired">Whether the CLI rejects the command line when the option is missing.</param>
public sealed record OptionDescriptor(
    string Name,
    IReadOnlyList<string> Aliases,
    string Description,
    OptionValueKind Kind,
    IReadOnlyList<string> AcceptedValues,
    bool IsRepeatable,
    bool IsGlobal,
    bool IsRequired);
