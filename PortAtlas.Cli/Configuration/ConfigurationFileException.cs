namespace PortAtlas.Cli.Configuration;

/// <summary>
/// Raised when the configuration file contains a setting that must not be ignored, such as a removed key whose
/// former value would change what the agent sends on the network.
/// </summary>
public sealed class ConfigurationFileException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    public ConfigurationFileException()
    {
    }

    /// <summary>
    /// Creates the exception with a message naming the faulty setting.
    /// </summary>
    /// <param name="message">Message shown to the operator.</param>
    public ConfigurationFileException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Creates the exception with a message and the underlying cause.
    /// </summary>
    /// <param name="message">Message shown to the operator.</param>
    /// <param name="innerException">Underlying cause.</param>
    public ConfigurationFileException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
