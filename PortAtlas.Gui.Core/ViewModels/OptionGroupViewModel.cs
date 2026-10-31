using PortAtlas.Gui.Core.Catalog;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// A non-empty section of options in a command form.
/// </summary>
/// <param name="Group">Section identifier.</param>
/// <param name="Options">Option view models belonging to this section.</param>
public sealed record OptionGroupViewModel(OptionGroup Group, IReadOnlyList<OptionViewModel> Options);
