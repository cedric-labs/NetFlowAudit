namespace PortAtlas.Gui.Core.Forms;

/// <summary>
/// The command line produced from a form: the argument array that is run, and its display form for the preview.
/// </summary>
/// <param name="Arguments">Arguments handed to the executable, one array element per argument; never a shell string.</param>
/// <param name="Preview">The same arguments quoted for display and copy; it is not what is executed.</param>
public sealed record BuiltCommandLine(IReadOnlyList<string> Arguments, string Preview);
