namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// A positional argument of a command, as declared by the command tree of the CLI.
/// </summary>
/// <param name="Name">Name of the argument, for example <c>ip</c>.</param>
/// <param name="Description">Help text of the argument.</param>
/// <param name="IsRequired">Whether the CLI rejects the command line when the argument is missing.</param>
public sealed record ArgumentDescriptor(string Name, string Description, bool IsRequired);
