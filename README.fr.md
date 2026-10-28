> **Language / Langue**: [English](README.md) · **Français**

# NetFlowAudit

<p align="center">
  <img src="assets/netflowaudit-icon.png" alt="Logo NetFlowAudit" width="200" />
  <br/>
  <a href="https://ko-fi.com/curvecoder"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Soutenir curvecoder sur Ko-fi" /></a>
</p>

NetFlowAudit enregistre avec quels services une machine Windows ou Linux communique, sur des jours ou des semaines, et quel programme en est à l'origine. Il lit à intervalle fixe les tables de sockets du système, rattache chaque socket à son processus et garde une ligne par flux dans un journal CSV. À partir de ces journaux, il produit des cartes topologiques HTML consultables hors ligne, des diagrammes Draw.io et des matrices de règles de pare-feu.

> [!NOTE]
> NetFlowAudit est né de l'envie de voir ce qui tournait et communiquait réellement sur une installation personnelle (serveurs Proxmox, un PC Windows, un poste Linux). Il est partagé en espérant qu'il serve à d'autres.

## Pourquoi

Préparer une migration, écrire des règles de pare-feu ou examiner un serveur pose la même question : à quoi cette machine se connecte-t-elle, et qu'est-ce qui écoute dessus ? La capture de paquets demande un pilote et produit de gros fichiers sans le processus derrière chaque connexion ; `netstat` montre un instant et manque les tâches planifiées ; journaliser chaque connexion sature le disque. NetFlowAudit tourne en arrière-plan, sans pilote, et garde un historique dédupliqué avec le programme, sa ligne de commande et l'application qu'il lance (`java -jar`, `python`, `powershell`…).

<p align="center">
  <img src="assets/tui-dashboard-preview.png" alt="Tableau de bord terminal de NetFlowAudit" width="100%" />
</p>

<p align="center">
  <img src="assets/topology-map-preview.png" alt="Carte topologique HTML de NetFlowAudit" width="100%" />
</p>

## Installer

Téléchargez l'archive de votre système sur la [page des versions](https://github.com/curvecoder/NetFlowAudit/releases) (`NetFlowAudit-<version>-win-x64.zip` ou `NetFlowAudit-<version>-linux-x64.tar.gz`) et décompressez-la. L'exécutable embarque le runtime .NET.

Pour compiler depuis les sources (SDK .NET 8) :

```bash
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
```

Utilisez `-r win-x64` pour Windows.

## Utiliser

Auditer la machine avec le tableau de bord (en administrateur sous Windows, avec `sudo` sous Linux) :

```bash
NetFlowAudit.Cli audit
```

Dessiner une carte à partir des journaux de plusieurs serveurs :

```bash
NetFlowAudit.Cli map -i "collected/audit_*.csv" --html fleet.html
```

Exporter en matrice de règles de pare-feu les flux internes vus au moins cinq fois :

```bash
NetFlowAudit.Cli export rules -i collected -o rules.csv --zone internal --min-observations 5
```

## Documentation

| | |
| :--- | :--- |
| Tutoriel | [Premiers pas](docs/getting-started.fr.md) |
| Guides pratiques | [Exécuter en service](docs/how-to/run-as-a-service.fr.md) · [Cartographier un parc de serveurs](docs/how-to/map-a-server-fleet.fr.md) · [Exporter des règles de pare-feu](docs/how-to/export-firewall-rules.fr.md) · [Auditer des serveurs isolés](docs/how-to/audit-isolated-servers.fr.md) · [Diagnostiquer un nom d'hôte manquant](docs/how-to/diagnose-name-resolution.fr.md) · [Passer de la 1.x à la 2.0](docs/how-to/upgrade-to-2.0.fr.md) |
| Référence | [Ligne de commande](docs/reference/cli.fr.md) · [Fichier de configuration](docs/reference/configuration.fr.md) · [Formats de sortie](docs/reference/output-formats.fr.md) · [Tableau de bord](docs/reference/dashboard.fr.md) |
| Explication | [Fonctionnement de NetFlowAudit](docs/explanation/architecture.fr.md) · [Décisions d'architecture](docs/adr/README.md) |

## Développement

`./verify.ps1` exécute le contrôle du formatage, la compilation Release avec avertissements bloquants, les tests avec couverture et l'analyse SonarQube.

## Soutien

Si NetFlowAudit vous est utile, vous pouvez soutenir sa maintenance sur [Ko-fi](https://ko-fi.com/curvecoder).

## Licence

[MIT](LICENSE)
