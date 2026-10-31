namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// An executable command of the CLI with everything needed to build its form.
/// </summary>
/// <param name="Path">Command names from the root, for example <c>export</c>, <c>rules</c>.</param>
/// <param name="Description">Help text of the command.</param>
/// <param name="Options">Options of the command, followed by the global options inherited from its ancestors.</param>
/// <param name="Arguments">Positional arguments, in the order the CLI expects them.</param>
public sealed record CommandDescriptor(
    IReadOnlyList<string> Path,
    string Description,
    IReadOnlyList<OptionDescriptor> Options,
    IReadOnlyList<ArgumentDescriptor> Arguments)
{
    /// <summary>
    /// Command path joined by spaces, as typed after the executable name; also the key of the command in a profile.
    /// </summary>
    public string Key => string.Join(' ', Path);
}
