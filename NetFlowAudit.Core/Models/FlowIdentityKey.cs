namespace NetFlowAudit.Core.Models;

/// <summary>
/// Clé d'identité immuable d'un flux réseau encapsulant sa signature logique sans obsession des types primitifs.
/// </summary>
public readonly record struct FlowIdentityKey
{
    /// <summary>
    /// Valeur normalisée de la signature logique du flux.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Initialise une nouvelle clé d'identité de flux à partir de sa signature.
    /// </summary>
    /// <param name="value">Signature brute du flux.</param>
    /// <exception cref="ArgumentException">Lancée si la signature est vide ou composée uniquement d'espaces.</exception>
    public FlowIdentityKey(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value.Trim();
    }

    /// <summary>
    /// Crée une clé d'identité à partir d'une entrée d'audit.
    /// </summary>
    /// <param name="entry">Entrée d'audit dont on extrait la clé logique.</param>
    /// <returns>Instance de la clé d'identité du flux.</returns>
    /// <exception cref="ArgumentNullException">Lancée si l'entrée d'audit est nulle.</exception>
    public static FlowIdentityKey FromEntry(NetworkAuditEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return new FlowIdentityKey(entry.ComputeAuditKey());
    }

    /// <inheritdoc />
    public override string ToString() => Value;
}
