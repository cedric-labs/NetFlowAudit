namespace PortAtlas.Core.Models;

/// <summary>
/// Immutable flow identity key encapsulating logical flow signature without primitive obsession.
/// </summary>
public readonly record struct FlowIdentityKey
{
    /// <summary>
    /// Normalized value of the logical flow signature.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initializes a new flow identity key from its raw signature string.
    /// </summary>
    /// <param name="value">Raw logical signature of the flow.</param>
    /// <exception cref="ArgumentException">Thrown when signature is null, empty, or whitespace.</exception>
    public FlowIdentityKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>
    /// Creates a flow identity key from a network audit entry.
    /// </summary>
    /// <param name="entry">Audit entry to extract the logical key from.</param>
    /// <returns>Flow identity key instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when entry is null.</exception>
    public static FlowIdentityKey FromEntry(NetworkAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new FlowIdentityKey(entry.ComputeAuditKey());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
