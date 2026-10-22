namespace NetFlowAudit.Core.Filtering;

/// <summary>
/// Règles d'exclusion et de filtrage paramétrables pour ignorer certains processus ou flux.
/// </summary>
public sealed record ProcessFilterOptions
{
    /// <summary>
    /// Liste des identifiants de processus (PID) à exclure systématiquement.
    /// </summary>
    public IReadOnlyList<int> ExcludedProcessIds { get; init; } = [0, 4];

    /// <summary>
    /// Noms exacts de processus à exclure (insensible à la casse).
    /// </summary>
    public IReadOnlyList<string> ExcludedExactNames { get; init; } = ["System", "Idle"];

    /// <summary>
    /// Préfixes de noms de processus à exclure (insensible à la casse).
    /// </summary>
    public IReadOnlyList<string> ExcludedNamePrefixes { get; init; } = [];

    /// <summary>
    /// Suffixes de noms de processus à exclure (insensible à la casse).
    /// </summary>
    public IReadOnlyList<string> ExcludedNameSuffixes { get; init; } = [];

    /// <summary>
    /// Segments de noms de processus à exclure (insensible à la casse).
    /// </summary>
    public IReadOnlyList<string> ExcludedNameContains { get; init; } = [];

    /// <summary>
    /// Détermine si les processus système de type NT AUTHORITY / SYSTEM doivent être ignorés.
    /// </summary>
    public bool ExcludeSystemAccounts { get; init; } = true;
}
