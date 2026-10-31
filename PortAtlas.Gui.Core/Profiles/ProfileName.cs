namespace PortAtlas.Gui.Core.Profiles;

/// <summary>
/// Name of a profile, which is also its file name: it cannot hold a path separator or any character a file name rejects.
/// </summary>
public readonly record struct ProfileName
{
    /// <summary>
    /// Validates and wraps a profile name.
    /// </summary>
    /// <param name="value">Name typed by the user.</param>
    /// <exception cref="ArgumentException">When the name is blank or holds a character that a file name cannot hold.</exception>
    public ProfileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A profile name cannot be blank.", nameof(value));
        }

        if (value.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || value.Trim() != value)
        {
            throw new ArgumentException($"'{value}' is not a valid profile name: it cannot hold path separators, reserved characters or edge spaces.", nameof(value));
        }

        Value = value;
    }

    /// <summary>
    /// The name as typed.
    /// </summary>
    public string Value { get; }

    /// <inheritdoc />
    public override string ToString() => Value;
}
