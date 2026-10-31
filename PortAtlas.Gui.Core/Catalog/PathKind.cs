namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// What a path option designates, which decides the picker the GUI offers.
/// </summary>
public enum PathKind
{
    /// <summary>The option is not a path.</summary>
    None,

    /// <summary>An existing file, directory or wildcard pattern that is read.</summary>
    FileToRead,

    /// <summary>An existing file or a directory; the GUI offers both pickers.</summary>
    FileOrDirectoryToRead,

    /// <summary>A file that is created or overwritten.</summary>
    FileToWrite,

    /// <summary>A directory.</summary>
    Directory
}
