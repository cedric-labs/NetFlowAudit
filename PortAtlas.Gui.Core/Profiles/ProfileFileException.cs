namespace PortAtlas.Gui.Core.Profiles;

/// <summary>
/// A profile file exists but cannot be read, for example because a crash truncated it.
/// </summary>
public sealed class ProfileFileException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    public ProfileFileException()
        : base("A profile file cannot be read.")
    {
    }

    /// <summary>
    /// Creates the exception with a message.
    /// </summary>
    /// <param name="message">Description of the failure.</param>
    public ProfileFileException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and its cause.
    /// </summary>
    /// <param name="message">Description of the failure.</param>
    /// <param name="innerException">Cause of the failure.</param>
    public ProfileFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Path of the unreadable file, when known.
    /// </summary>
    public string? FilePath { get; private init; }

    /// <summary>
    /// Creates the exception for an unreadable file.
    /// </summary>
    /// <param name="filePath">Path of the unreadable file.</param>
    /// <param name="innerException">Cause of the failure.</param>
    /// <returns>An exception whose message names the file.</returns>
    public static ProfileFileException ForFile(string filePath, Exception innerException) =>
        new($"The profile file '{filePath}' cannot be read: {innerException.Message}", innerException) { FilePath = filePath };
}
