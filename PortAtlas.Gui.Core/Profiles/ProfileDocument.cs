using PortAtlas.Gui.Core.Forms;

namespace PortAtlas.Gui.Core.Profiles;

/// <summary>
/// The content of a profile: one form state per command.
/// </summary>
public sealed class ProfileDocument
{
    /// <summary>
    /// Form states keyed by command key (see <see cref="Catalog.CommandDescriptor.Key"/>).
    /// </summary>
    public Dictionary<string, CommandFormState> Forms { get; init; } = [];
}
