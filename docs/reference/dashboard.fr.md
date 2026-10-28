> **Language / Langue**: [English](dashboard.md) · **Français**

# Référence du tableau de bord

`NetFlowAudit.Cli audit` affiche un tableau de bord plein écran dans le terminal, sauf avec `--no-tui`, avec `EnableDashboard` à `false`, ou quand l'entrée ou la sortie du processus est redirigée.

## Vues

| Vue | Contenu |
| :--- | :--- |
| Audit Log | Flux dédupliqués enregistrés depuis le démarrage, y compris ceux rechargés depuis le journal CSV |
| Active Sockets | Sockets vus par le dernier balayage, avant application des filtres d'enregistrement |

## Touches

| Touche | Action |
| :--- | :--- |
| `Q` | Arrêter l'audit et quitter |
| `C` | Redessiner l'écran |
| `V` ou `F` | Basculer entre les deux vues |
| `M` | Écrire la carte HTML (`OutputHtmlMapPath`, ou `netflow_topology.html`), et le diagramme Draw.io quand `OutputDrawioPath` est renseigné |
| `↑` `↓` `←` `→` | Déplacer la sélection dans le tableau |
| `N` ou `Page Down` | Page suivante |
| `P` ou `Page Up` | Page précédente |
| `L` ou `+` | Charger 100 lignes de plus |
| `A` | Charger toutes les lignes |
| `Enter`, `X` ou `E` | Exclure la valeur de la cellule sélectionnée |
| `Esc` | Annuler la sélection |

`Ctrl+C` et `SIGTERM` arrêtent l'audit comme `Q`.

## Exclure depuis le tableau de bord

Exclure une cellule ajoute sa valeur au fichier de configuration donné par `--config` (ou celui par défaut) et l'applique aussitôt :

| Colonne sélectionnée | Clé mise à jour |
| :--- | :--- |
| Port local | `ExcludedLocalPorts` |
| Adresse distante | `ExcludedRemoteIps` |
| Port distant | `ExcludedRemotePorts` |
| Processus | `ExcludedExactNames` |
| Cible | `ExcludedExactNames` |

## Masquer pour des captures d'écran

`--mask-server`, `--mask-ip` et `--obfuscate` ne changent que l'affichage du tableau de bord ; le journal d'audit garde les vraies valeurs.
