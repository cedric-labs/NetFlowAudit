> **Language / Langue**: [English](upgrade-to-3.0.md) · **Français**

# Passer de la 2.x à la 3.0

PortAtlas 3.0 est le nouveau nom de NetFlowAudit. Le comportement fonctionnel reste identique, mais le nom de l'exécutable, le nom par défaut du fichier de configuration et l'adresse du dépôt changent. Mettez à jour scripts, tâches planifiées, services et unités avant de déployer la 3.0.

## Nom de l'exécutable

L'exécutable s'appelle désormais `PortAtlas` au lieu de `NetFlowAudit.Cli` :

| Système d'exploitation | 2.x | 3.0 |
| :--- | :--- | :--- |
| Windows | `NetFlowAudit.Cli.exe` | `PortAtlas.exe` |
| Linux | `NetFlowAudit.Cli` | `PortAtlas` |

Mettez à jour vos variables PATH, définitions de service (unités systemd, services Windows ou Planificateur de tâches) et scripts automatisés pour pointer vers `PortAtlas.exe` (ou `PortAtlas`).

## Fichier de configuration

Le fichier de configuration par défaut s'appelle désormais `portatlas.json` au lieu de `netflowaudit.json` :

- Renommez votre fichier existant `netflowaudit.json` en `portatlas.json`.
- Aucun repli automatique n'est appliqué : PortAtlas ne lit pas `netflowaudit.json`. Si seul `netflowaudit.json` existe et qu'aucune option `--config` n'est fournie, PortAtlas applique les paramètres par défaut intégrés.
- Si vous générez un nouveau modèle de configuration avec `config init`, le fichier créé est `portatlas.json`.

## Adresse du dépôt

Le dépôt du projet change d'adresse :

- Ancienne adresse : `https://github.com/curvecoder/NetFlowAudit`
- Nouvelle adresse : `https://github.com/curvecoder/PortAtlas`

Mettez à jour vos remotes Git, favoris de documentation et liens de téléchargement.

## Cartes HTML

Les cartes générées avec la 2.x proposaient une commande commençant par `NetFlowAudit.Cli map` dans leur zone de copie. Régénérer les cartes avec la 3.0 produit la syntaxe de commande `PortAtlas map`.
