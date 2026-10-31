namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// Shape of the value an option takes, which decides the form control generated for it.
/// </summary>
public enum OptionValueKind
{
    /// <summary>The option takes no value: it is present or absent.</summary>
    Flag,

    /// <summary>The option takes one free text value.</summary>
    Text,

    /// <summary>The option takes one integer value.</summary>
    Integer,

    /// <summary>The option takes one value among a closed set exposed by <see cref="OptionDescriptor.AcceptedValues"/>.</summary>
    Choice,

    /// <summary>The option takes several values, given by repeating the option.</summary>
    TextList
}
