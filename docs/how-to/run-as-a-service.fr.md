> **Language / Langue**: [English](run-as-a-service.md) · **Français**

# Exécuter PortAtlas en service

L'audit ne voit que les connexions ouvertes au moment d'un balayage : il est donc surtout utile quand il tourne pendant des jours. Ces étapes le démarrent avec la machine, sans tableau de bord.

Les exemples installent le programme dans `C:\Program Files\PortAtlas` (Windows) et `/opt/portatlas` (Linux), avec le fichier de configuration à côté de l'exécutable. Adaptez les chemins à votre organisation.

## Windows : tâche planifiée

Aucun logiciel supplémentaire n'est nécessaire. Dans un PowerShell administrateur :

```powershell
$action = New-ScheduledTaskAction -Execute "C:\Program Files\PortAtlas\PortAtlas.exe" -Argument 'audit --no-tui --config "C:\Program Files\PortAtlas\portatlas.json"' -WorkingDirectory "C:\ProgramData\PortAtlas"
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit 0
Register-ScheduledTask -TaskName "PortAtlas" -Action $action -Trigger $trigger -Principal $principal -Settings $settings
Start-ScheduledTask -TaskName "PortAtlas"
```

Créez d'abord `C:\ProgramData\PortAtlas`. Les chemins relatifs de la configuration (`OutputCsvPath`, `DnsCacheFilePath`) sont relatifs au dossier de travail.

## Windows : service avec NSSM

[NSSM](https://nssm.cc) exécute un programme console comme service Windows. Installez-le (`winget install NSSM.NSSM`), puis dans une invite administrateur :

```powershell
nssm install PortAtlas "C:\Program Files\PortAtlas\PortAtlas.exe"
nssm set PortAtlas AppParameters 'audit --no-tui --config "C:\Program Files\PortAtlas\portatlas.json"'
nssm set PortAtlas AppDirectory "C:\ProgramData\PortAtlas"
nssm set PortAtlas AppStdout "C:\ProgramData\PortAtlas\service.log"
nssm set PortAtlas AppStderr "C:\ProgramData\PortAtlas\service_error.log"
nssm set PortAtlas Start SERVICE_AUTO_START
nssm start PortAtlas
```

## Linux : unité systemd

Créez `/etc/systemd/system/portatlas.service` :

```ini
[Unit]
Description=PortAtlas network flow audit
After=network-online.target

[Service]
ExecStart=/opt/portatlas/PortAtlas audit --no-tui --config /opt/portatlas/portatlas.json
WorkingDirectory=/var/lib/portatlas
Restart=on-failure
User=root

[Install]
WantedBy=multi-user.target
```

Puis :

```bash
sudo mkdir -p /var/lib/portatlas
sudo systemctl daemon-reload
sudo systemctl enable --now portatlas
```

L'unité tourne en root pour que chaque socket soit rattaché à son processus ([pourquoi](../explanation/architecture.fr.md#lire-les-sockets-plutôt-que-capturer-les-paquets)).

## Vérifier le fonctionnement

Après quelques balayages, le journal CSV (`netflow_audit.csv` dans le dossier de travail, ou `OutputCsvPath`) contient des lignes. Sans tableau de bord, l'agent affiche ses réglages au démarrage et `Graceful shutdown of network flow audit.` à l'arrêt ; avec NSSM ces lignes vont dans `service.log`, avec systemd dans `journalctl -u portatlas`.

La commande doit nommer `audit` : lancé sans commande, PortAtlas 2.0 affiche l'aide et sort avec le code 0. Une unité en `Restart=on-failure` reste alors arrêtée.
