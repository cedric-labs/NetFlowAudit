> **Language / Langue**: **English** · [Français](run-as-a-service.fr.md)

# Run NetFlowAudit as a service

The audit only sees connections open at the moment of a scan, so it is most useful when it runs for days. These steps start it with the machine, without the dashboard.

The examples install the program in `C:\Program Files\NetFlowAudit` (Windows) and `/opt/netflowaudit` (Linux), with the configuration file next to the executable. Adapt the paths to your layout.

## Windows: scheduled task

No extra software is needed. In an elevated PowerShell:

```powershell
$action = New-ScheduledTaskAction -Execute "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe" -Argument 'audit --no-tui --config "C:\Program Files\NetFlowAudit\netflowaudit.json"' -WorkingDirectory "C:\ProgramData\NetFlowAudit"
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit 0
Register-ScheduledTask -TaskName "NetFlowAudit" -Action $action -Trigger $trigger -Principal $principal -Settings $settings
Start-ScheduledTask -TaskName "NetFlowAudit"
```

Create `C:\ProgramData\NetFlowAudit` first. Relative paths of the configuration (`OutputCsvPath`, `DnsCacheFilePath`) are relative to the working directory.

## Windows: service with NSSM

[NSSM](https://nssm.cc) runs a console program as a Windows service. Install it (`winget install NSSM.NSSM`), then in an elevated prompt:

```powershell
nssm install NetFlowAudit "C:\Program Files\NetFlowAudit\NetFlowAudit.Cli.exe"
nssm set NetFlowAudit AppParameters 'audit --no-tui --config "C:\Program Files\NetFlowAudit\netflowaudit.json"'
nssm set NetFlowAudit AppDirectory "C:\ProgramData\NetFlowAudit"
nssm set NetFlowAudit AppStdout "C:\ProgramData\NetFlowAudit\service.log"
nssm set NetFlowAudit AppStderr "C:\ProgramData\NetFlowAudit\service_error.log"
nssm set NetFlowAudit Start SERVICE_AUTO_START
nssm start NetFlowAudit
```

## Linux: systemd unit

Create `/etc/systemd/system/netflowaudit.service`:

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

Then:

```bash
sudo mkdir -p /var/lib/netflowaudit
sudo systemctl daemon-reload
sudo systemctl enable --now netflowaudit
```

The unit runs as root so that every socket is attributed to its process ([why](../explanation/architecture.md#reading-sockets-instead-of-capturing-packets)).

## Check that it runs

After a few scans, the CSV log (`netflow_audit.csv` in the working directory, or `OutputCsvPath`) contains lines. Without the dashboard, the agent prints its settings at startup and `Graceful shutdown of network flow audit.` when it stops; with NSSM these lines go to `service.log`, with systemd to `journalctl -u netflowaudit`.

The command must name `audit`: started without a command, NetFlowAudit 2.0 prints the help and exits with code 0. A unit with `Restart=on-failure` then stays stopped.
