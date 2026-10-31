using PortAtlas.Gui.Core.Catalog;

namespace PortAtlas.Gui.Core.Interaction;

/// <summary>
/// Opens a system dialog for picking a file or a directory.
/// </summary>
public interface IFilePickerService
{
    /// <summary>
    /// Prompts the user to pick a path matching <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">Kind of path to pick.</param>
    /// <param name="cancellationToken">Token that cancels the dialog.</param>
    /// <returns>The chosen path, or <see langword="null"/> when the user cancels.</returns>
    Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default);
}
