namespace PortAtlas.Gui.Core.Profiles;

/// <summary>
/// Settings the GUI restores at start-up.
/// </summary>
/// <param name="Theme">Colour theme.</param>
/// <param name="LastProfile">Name of the profile loaded or saved last, if any.</param>
public sealed record UserPreferences(ThemeChoice Theme = ThemeChoice.System, string? LastProfile = null);
