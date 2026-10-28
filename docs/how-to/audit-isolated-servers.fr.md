> **Language / Langue**: [English](audit-isolated-servers.md) · **Français**

# Auditer des serveurs sans accès Internet

Objectif : auditer des serveurs qui n'atteignent pas Internet, puis nommer leurs adresses distantes depuis un poste connecté.

## Sur chaque serveur isolé

Lancez l'audit sans requêtes RDAP, qui ne feraient qu'expirer :

```bash
NetFlowAudit.Cli audit --no-tui --no-rdap
```

Ou mettez `"ResolveIpOwners": false` dans le fichier de configuration. Le DNS inverse reste actif et nomme les adresses connues des serveurs DNS internes. Pour n'envoyer aucune requête de nom, utilisez plutôt `--no-dns`.

Collectez les journaux d'audit et les fichiers de cache DNS (`netflow_dns_cache.json`, ou `DnsCacheFilePath`) de chaque serveur.

## Sur un poste connecté

Fusionnez les caches, résolvez les adresses encore sans nom et écrivez des copies enrichies des journaux :

```bash
NetFlowAudit.Cli resolve -i "collected/*.csv" --cache "collected/*.json" --output-cache master_cache.json
```

Chaque `audit_x.csv` donne un `audit_x_enriched.csv` ; `--in-place` remplace plutôt les originaux. Les requêtes RDAP sont espacées de 200 ms (`--rdap-delay-ms`).

## Sans aucun accès réseau

Pour n'utiliser que les caches collectés, sans requête DNS ni RDAP :

```bash
NetFlowAudit.Cli resolve -i "collected/*.csv" --cache master_cache.json --cache-only
```

## Venir de la 1.x

Le mode isolé de la 1.x n'existe plus. Un fichier de configuration avec `"IsolatedMode": true` arrête NetFlowAudit avec une erreur tant que la clé n'est pas remplacée par `"ResolveIpOwners": false` ([guide de migration](upgrade-to-2.0.fr.md)).
