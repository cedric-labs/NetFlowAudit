> **Language / Langue**: [English](export-firewall-rules.md) · **Français**

# Exporter une matrice de règles de pare-feu

Objectif : une liste CSV des flux à autoriser, une ligne par source, destination, port et processus, pour préparer des règles de pare-feu ou une migration.

## Exporter les flux observés

```bash
NetFlowAudit.Cli export rules -i collected -o firewall_rules.csv
```

`-i` lit les journaux CSV et NDJSON (un dossier, un motif ou des fichiers). Les ports de destination éphémères (49152 et au-delà) sont écartés.

## Réduire le bruit

Garder les flux vus au moins cinq fois pendant les 14 derniers jours, vers des adresses internes, sans DNS ni NTP :

```bash
NetFlowAudit.Cli export rules -i collected -o internal_rules.csv --min-observations 5 --active-within-days 14 --zone internal --exclude-ports 53,123
```

`--zone public` ne garde que les destinations hors des plages privées, par exemple pour lister les services Internet dont un serveur a besoin.

## Lister les services proposés

```bash
NetFlowAudit.Cli export rules -i collected -o services.csv --listeners-only
```

Chaque socket en écoute devient une règle entrante depuis n'importe quelle source (`0.0.0.0`, hôte `Any`) vers son adresse et son port : le point de départ d'un jeu de règles entrantes.

## Lire le résultat

La commande affiche le nombre de fichiers lus, de flux examinés et de règles écrites. Chaque ligne porte le processus, son chemin et les dates d'observation, pour justifier chaque règle ([liste des colonnes](../reference/output-formats.fr.md#matrice-de-règles-de-pare-feu)).
