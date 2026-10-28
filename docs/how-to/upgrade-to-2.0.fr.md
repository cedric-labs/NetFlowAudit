> **Language / Langue**: [English](upgrade-to-2.0.md) · **Français**

# Passer de la 1.x à la 2.0

NetFlowAudit 2.0 remplace les options de la 1.x par des commandes. Les lignes de commande 1.x sont refusées : mettez à jour scripts, tâches planifiées, services et unités avant de déployer la 2.0.

## Services et tâches planifiées

Un service 1.x lançait l'audit sans argument, ou avec `--daemon` / `--no-tui`. En 2.0, le programme lancé sans commande affiche l'aide et sort avec le code 0. Remplacez les arguments par :

```text
audit --no-tui --config <path>
```

## Correspondance des commandes

| 1.x | 2.0 |
| :--- | :--- |
| *(aucune option)* | `audit` |
| `--daemon`, `--no-tui`, `--silent` | `audit --no-tui` |
| `--isolated`, `--offline`, `--no-ip-owners`, `--resolve-ip-owners false` | `audit --no-rdap` |
| `--resolve-dns false` | `audit --no-dns` |
| `--map-only -i <file>` | `map -i <file>` |
| `--map <path>` | `audit --html <path>` ou `map --html <path>` |
| `--export-drawio <path>`, `--drawio <path>` | `map --drawio <path>` |
| `--input-dir <dir> --input-filter <pattern>` | `-i "<dir>/<pattern>"` |
| `--inputs a.csv,b.csv` | `-i a.csv -i b.csv` |
| `--map-layout`, `--map-sort`, `--map-routing`, `--map-spacing` | `--layout`, `--sort`, `--routing`, `--spacing` |
| `--drawio-edge-mode` | `--drawio-edges` |
| `--export-rules <path>` | `export rules -o <path>` |
| `--internal-only`, `--public-only`, `--destination-zone <zone>` | `export rules --zone internal` ou `--zone public` |
| `--exclude-ephemeral` | comportement par défaut ; `--include-ephemeral-ports` les conserve |
| `--export-ndjson <path>` | `export ndjson -o <path>` |
| `--ndjson <path>`, `--output-ndjson <path>` | `audit --ndjson <path>` |
| `--resolve-offline` | `resolve` |
| `--resolve-offline --isolated` | `resolve --cache-only` |
| `--caches a.json,b.json` | `resolve --cache a.json --cache b.json` |
| `--test-ip <ip>`, `--lookup <ip>` | `lookup <ip>` |
| `--init-config`, `--generate-config` | `config init` |
| `--anonymize` | `audit --obfuscate` |
| `--obfuscate-server`, `--obfuscate-ip` | `audit --mask-server`, `audit --mask-ip` |
| `--history-limit` | `audit --reload-history` |
| `--aliases` | `audit --alias` (répétable) |
| `--exclude-remote-ip` | `audit --exclude-remote-ips` |

Les alias courts des options de filtre (`--exclude-host`, `--include-port`…) disparaissent ; utilisez les formes au pluriel. Les valeurs hors des listes admises, comme `--direction listening` ou `--layout force`, sont refusées.

## Fichier de configuration

| Changement | Action |
| :--- | :--- |
| `IsolatedMode` supprimée | Remplacez `"IsolatedMode": true` par `"ResolveIpOwners": false` ; avec `true`, la 2.0 refuse de démarrer |
| `UserName` renseigné, `ExcludeSystemAccounts` à `false` par défaut | Le compte de chaque processus est désormais enregistré. `ExcludeSystemAccounts`, sans effet en 1.x, fonctionne maintenant : laissez-le à `false` pour enregistrer les services exécutés par `SYSTEM`, `NT AUTHORITY` ou `root` |
| `config init` écrit les valeurs par défaut | Les fichiers générés par la 1.x contenaient des exclusions d'exemple (`ExcludedRemoteIps: ["192.168.1.50", "10.0.0.0/8"]`, `ExcludedLocalPorts: [22, 3389]`…). Relisez-les : elles empêchent l'enregistrement de ces flux |

## Cartes HTML

Les cartes produites par la 1.x proposent une commande 1.x dans leur section `CLI Command`. Régénérez-les avec la 2.0 pour obtenir la syntaxe `map`.
