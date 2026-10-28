> **Language / Langue**: [English](map-several-machines.md) · **Français**

# Cartographier plusieurs machines

Objectif : une seule carte topologique montrant comment plusieurs serveurs communiquent entre eux.

## Donner à chaque serveur son propre nom de fichier

Déployez le même fichier de configuration sur chaque serveur, avec le nom de la machine dans le chemin de sortie :

```json
{
  "OutputCsvPath": "audit_{ServerName}.csv"
}
```

`{ServerName}` devient le nom de chaque machine : les fichiers collectés ne s'écrasent pas. `{Date}` peut être ajouté pour démarrer un nouveau fichier chaque jour. Faites tourner l'audit en service sur chaque serveur ([comment](run-as-a-service.fr.md)).

## Collecter les fichiers

Copiez les journaux CSV de tous les serveurs dans un même dossier, par exemple `collected/`, avec l'outil que vous utilisez déjà (partage réseau, `scp`, Syncthing).

## Dessiner la carte

```bash
NetFlowAudit.Cli map -i "collected/audit_*.csv" --html homelab.html
```

`-i` accepte un motif, un dossier ou un fichier, et peut être répété.

Pour se concentrer sur les bases de données :

```bash
NetFlowAudit.Cli map -i collected --include-ports 3306,5432,6379,27017 --html databases.html
```

Pour un diagramme modifiable, ajoutez `--drawio homelab.drawio`. Pour réorganiser un grand diagramme, sélectionnez tout et appliquez `Arrange > Layout > Hierarchical` dans Draw.io.

## Reproduire une vue

La section `CLI Command` de la carte HTML affiche la commande `map` qui reproduit les filtres et choix de disposition en cours. Copiez-la et ajoutez l'option `-i` de vos journaux d'audit.
