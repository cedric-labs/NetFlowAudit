using System.Text.Json;

namespace NetFlowAudit.Core.Resolution;

/// <summary>
/// Analyseur syntaxique de réponses JSON RDAP (RFC 7483) extrayant l'organisation ou entité propriétaire d'une adresse IP.
/// </summary>
public static class RdapResponseParser
{
    private const int MaxOrganizationLength = 80;

    /// <summary>
    /// Extrait le nom de l'organisation ou du détenteur d'un bloc IP à partir d'un document RDAP.
    /// </summary>
    /// <param name="jsonContent">Contenu JSON brut renvoyé par le service RDAP.</param>
    /// <returns>Nom nettoyé de l'organisation ou null si introuvable.</returns>
    public static string? ExtractOrganization(string jsonContent)
    {
        if (string.IsNullOrWhiteSpace(jsonContent))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(jsonContent);
            var root = doc.RootElement;

            // 1. Recherche prioritaire dans les entités avec rôle 'registrant'
            if (root.TryGetProperty("entities", out var entities) && entities.ValueKind == JsonValueKind.Array)
            {
                var registrantOrg = FindOrganizationInEntities(entities, targetRole: "registrant");
                if (!string.IsNullOrWhiteSpace(registrantOrg))
                {
                    return SanitizeName(registrantOrg);
                }

                // 2. Recherche secondaire dans toute autre entité
                var anyOrg = FindOrganizationInEntities(entities, targetRole: null);
                if (!string.IsNullOrWhiteSpace(anyOrg))
                {
                    return SanitizeName(anyOrg);
                }
            }

            // 3. Repli sur le champ racine 'name' (ex: "MSFT", "FR-PROXAD")
            if (root.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String)
            {
                var nameVal = nameProp.GetString();
                if (!string.IsNullOrWhiteSpace(nameVal))
                {
                    return SanitizeName(nameVal);
                }
            }

            return null;
        }
        catch (JsonException)
        {
            // Toléré en cas de JSON tronqué ou malformé provenant du réseau
            return null;
        }
    }

    private static string? FindOrganizationInEntities(JsonElement entities, string? targetRole)
    {
        foreach (var entity in entities.EnumerateArray())
        {
            var roleMatches = targetRole is null;
            if (targetRole is not null && entity.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
            {
                foreach (var role in roles.EnumerateArray())
                {
                    if (role.ValueEquals(targetRole))
                    {
                        roleMatches = true;
                        break;
                    }
                }
            }

            if (!roleMatches)
            {
                continue;
            }

            // Extraction depuis vcardArray: ["vcard", [ [ "version", {}, "text", "4.0" ], [ "fn", {}, "text", "Name" ], ... ]]
            if (entity.TryGetProperty("vcardArray", out var vcardArray) &&
                vcardArray.ValueKind == JsonValueKind.Array &&
                vcardArray.GetArrayLength() >= 2)
            {
                var cardProps = vcardArray[1];
                if (cardProps.ValueKind == JsonValueKind.Array)
                {
                    string? candidateFn = null;
                    string? candidateOrg = null;

                    foreach (var prop in cardProps.EnumerateArray())
                    {
                        if (prop.ValueKind == JsonValueKind.Array && prop.GetArrayLength() >= 4)
                        {
                            var propName = prop[0].GetString();
                            if (string.Equals(propName, "org", StringComparison.OrdinalIgnoreCase))
                            {
                                candidateOrg = prop[3].GetString();
                            }
                            else if (string.Equals(propName, "fn", StringComparison.OrdinalIgnoreCase))
                            {
                                candidateFn = prop[3].GetString();
                            }
                        }
                    }

                    var selected = !string.IsNullOrWhiteSpace(candidateOrg) ? candidateOrg : candidateFn;
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        return selected;
                    }
                }
            }

            // Récursion éventuelle sur entités imbriquées
            if (entity.TryGetProperty("entities", out var nestedEntities) && nestedEntities.ValueKind == JsonValueKind.Array)
            {
                var nestedResult = FindOrganizationInEntities(nestedEntities, targetRole);
                if (!string.IsNullOrWhiteSpace(nestedResult))
                {
                    return nestedResult;
                }
            }
        }

        return null;
    }

    private static string SanitizeName(string raw)
    {
        var cleaned = raw.Trim();
        if (cleaned.Length > MaxOrganizationLength)
        {
            cleaned = cleaned[..MaxOrganizationLength].TrimEnd();
        }

        return cleaned;
    }
}
