> **Language / Langue**: [English](run-as-a-service.md) · **Français**

# Exécuter NetFlowAudit en service

L'audit ne voit que les connexions ouvertes au moment d'un balayage : il est donc surtout utile quand il tourne pendant des jours. Ces étapes le démarrent avec la machine, sans tableau de bord.

Les exemples installent le programme dans `C:\Program Files\NetFlowAudit` (Windows) et `/opt/netflowaudit` (Linux), avec le fichier de configuration à côté de l'exécutable. Adaptez les chemins à votre organisation.

## Windows : tâche planifiée

Aucun logiciel supplémentaire n'est nécessaire. Dans un PowerShell administrateur :

```powershell
$action = New-ScheduledTaskAction -Execute "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe" -Argument 'audit --no-tui --config "C:\Program Files\NetFlowAudit\netflowaudit.json"' -WorkingDirectory "C:\ProgramData\NetFlowAudit"
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit 0
Register-ScheduledTask -TaskName "NetFlowAudit" -Action $action -Trigger $trigger -Principal $principal -Settings $settings
Start-ScheduledTask -TaskName "NetFlowAudit"
```

Créez d'abord `C:\ProgramData\NetFlowAudit`. Les chemins relatifs de la configuration (`OutputCsvPath`, `DnsCacheFilePath`) sont relatifs au dossier de travail.

## Windows : service avec NSSM

[NSSM](https://nssm.cc) exécute un programme console comme service Windows. Installez-le (`winget install NSSM.NSSM`), puis dans une invite administrateur :

```powershell
nssm install NetFlowAudit "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe"
nssm set NetFlowAudit AppParameters 'audit --no-tui --config "C:\Program Files\NetFlowAudit\netflowaudit.json"'
nssm set NetFlowAudit AppDirectory "C:\ProgramData\NetFlowAudit"
nssm set NetFlowAudit AppStdout "C:\ProgramData\NetFlowAudit\service.log"
nssm set NetFlowAudit AppStderr "C:\ProgramData\NetFlowAudit\service_error.log"
nssm set NetFlowAudit Start SERVICE_AUTO_START
nssm start NetFlowAudit
```

## Linux : unité systemd

Créez `/etc/systemd/system/netflowaudit.service` :

```ini
[Unit]
Description=NetFlowAudit network flow audit
After=network-online.target

[Service]
ExecStart=/opt/netflowaudit/NetFlowAudit.Cli audit --no-tui --config /opt/netflowaudit/netflowaudit.json
WorkingDirectory=/var/lib/netflowaudit
Restart=on-failure
User=root

[Install]
WantedBy=multi-user.target
```

Puis :

```bash
sudo mkdir -p /var/lib/netflowaudit
sudo systemctl daemon-reload
sudo systemctl enable --now netflowaudit
```

L'unité tourne en root pour que chaque socket soit rattaché à son processus ([pourquoi](../explanation/architecture.fr.md#lire-les-sockets-plutôt-que-capturer-les-paquets)).

## Vérifier le fonctionnement

Après quelques balayages, le journal CSV (`netflow_audit.csv` dans le dossier de travail, ou `OutputCsvPath`) contient des lignes. Sans tableau de bord, l'agent affiche ses réglages au démarrage et `Graceful shutdown of network flow audit.` à l'arrêt ; avec NSSM ces lignes vont dans `service.log`, avec systemd dans `journalctl -u netflowaudit`.

La commande doit nommer `audit` : lancé sans commande, NetFlowAudit 2.0 affiche l'aide et sort avec le code 0. Une unité en `Restart=on-failure` reste alors arrêtée.
