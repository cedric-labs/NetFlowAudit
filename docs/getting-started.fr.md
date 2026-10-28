> **Language / Langue**: [English](getting-started.md) · **Français**

# Premiers pas

Ce tutoriel lance un premier audit sur votre machine et le transforme en carte topologique. Comptez une dizaine de minutes.

## Télécharger

Sur la [page des versions](https://github.com/curvecoder/NetFlowAudit/releases), téléchargez l'archive de votre système et décompressez-la dans un dossier :

- Windows : `NetFlowAudit-<version>-win-x64.zip`
- Linux : `NetFlowAudit-<version>-linux-x64.tar.gz`

L'exécutable est autonome : aucun runtime .NET n'est nécessaire. L'archive contient aussi un fichier `netflowaudit.json` avec les réglages par défaut.

## Lancer un premier audit

Ouvrez un terminal dans ce dossier, en administrateur sous Windows.

Windows (PowerShell) :

```powershell
.\NetFlowAudit.Cli.exe audit
```

Linux :

```bash
chmod +x ./NetFlowAudit.Cli
sudo ./NetFlowAudit.Cli audit
```

Le tableau de bord s'ouvre. Toutes les cinq secondes, il liste les flux trouvés : le programme, l'adresse et le port distants, et le nom résolu pour cette adresse. Ouvrez une page web ou lancez `git fetch` dans une autre fenêtre : de nouveaux flux apparaissent en quelques secondes.

Laissez l'audit tourner quelques minutes, puis appuyez sur `Q`. Les flux sont maintenant dans `netflow_audit.csv`, dans le même dossier.

## Dessiner la carte

Windows :

```powershell
.\NetFlowAudit.Cli.exe map --html first_map.html
```

Linux :

```bash
./NetFlowAudit.Cli map --html first_map.html
```

La commande affiche `Topology map generated: …` avec le nombre de serveurs et de flux. Ouvrez `first_map.html` dans un navigateur : votre machine apparaît avec les serveurs auxquels elle a parlé. La page fonctionne hors ligne.

## Et ensuite

- Faire tourner l'audit en permanence : [Exécuter NetFlowAudit en service](how-to/run-as-a-service.fr.md).
- Construire une carte pour plusieurs serveurs : [Cartographier un parc de serveurs](how-to/map-a-server-fleet.fr.md).
- Toutes les commandes et options : [référence de la ligne de commande](reference/cli.fr.md).
