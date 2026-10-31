namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// One accepted value of a choice option with its explanation.
/// </summary>
/// <param name="Value">Value passed to the command line.</param>
/// <param name="Description">What the value does.</param>
public sealed record ChoiceItem(string Value, string Description)
{
    /// <inheritdoc />
    public override string ToString() => Value;
}
