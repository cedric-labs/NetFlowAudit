namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// What the command tree does not say about an option: where to show it and which picker a path needs.
/// </summary>
/// <param name="Group">Section of the form.</param>
/// <param name="PathKind">Kind of path the option holds, or <see cref="PathKind.None"/>.</param>
public sealed record OptionPresentationEntry(OptionGroup Group, PathKind PathKind);
