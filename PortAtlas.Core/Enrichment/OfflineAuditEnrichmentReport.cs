using PortAtlas.Core.Resolution;

namespace PortAtlas.Core.Enrichment;

/// <summary>
/// Comprehensive outcome metrics generated upon completion of offline audit enrichment.
/// </summary>
/// <param name="ProcessedAuditFiles">List of original audit files processed.</param>
/// <param name="GeneratedAuditFiles">List of generated or modified audit files.</param>
/// <param name="TotalRecordsScanned">Gross number of connection rows/records examined.</param>
/// <param name="TotalRecordsUpdated">Number of rows where the remote hostname was updated.</param>
/// <param name="DistinctIpsEnriched">Number of unique IP addresses enriched.</param>
/// <param name="OnlineResolutionsCount">Number of real-time online DNS/RDAP queries performed.</param>
/// <param name="CacheReport">Cache consolidation and conflict analysis report.</param>
public sealed record OfflineAuditEnrichmentReport(
    IReadOnlyList<string> ProcessedAuditFiles,
    IReadOnlyList<string> GeneratedAuditFiles,
    int TotalRecordsScanned,
    int TotalRecordsUpdated,
    int DistinctIpsEnriched,
    int OnlineResolutionsCount,
    DnsCacheConsolidationReport CacheReport);
