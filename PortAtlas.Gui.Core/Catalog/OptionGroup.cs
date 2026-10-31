namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// Section of the form an option is shown in.
/// </summary>
public enum OptionGroup
{
    /// <summary>Audit logs and caches that are read.</summary>
    Inputs,

    /// <summary>Selection of the flows to keep.</summary>
    Filters,

    /// <summary>Look of the HTML map and of the Draw.io diagram.</summary>
    MapRendering,

    /// <summary>Files that are written.</summary>
    Outputs,

    /// <summary>Reverse DNS and RDAP resolution.</summary>
    Resolution,

    /// <summary>Options that do not belong to another section.</summary>
    Advanced
}
