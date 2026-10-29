> **Language / Langue**: **English** · [Français](run-as-a-service.fr.md)

# Run PortAtlas as a service

The audit only sees connections open at the moment of a scan, so it is most useful when it runs for days. These steps start it with the machine, without the dashboard.

The examples install the program in `C:\Program Files\PortAtlas` (Windows) and `/opt/portatlas` (Linux), with the configuration file next to the executable. Adapt the paths to your layout.

## Windows: scheduled task

No extra software is needed. In an elevated PowerShell:

```powershell
$action = New-ScheduledTaskAction -Execute "C:\Program Files\PortAtlas\PortAtlas.exe" -Argument 'audit --no-tui --config "C:\Program Files\PortAtlas\portatlas.json"' -WorkingDirectory "C:\ProgramData\PortAtlas"
$trigger = New-ScheduledTaskTrigger -AtStartup
$principal = New-ScheduledTaskPrincipal -UserId "NT AUTHORITY\SYSTEM" -LogonType ServiceAccount -RunLevel Highest
$settings = New-ScheduledTaskSettingsSet -RestartCount 3 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit 0
Register-ScheduledTask -TaskName "PortAtlas" -Action $action -Trigger $trigger -Principal $principal -Settings $settings
Start-ScheduledTask -TaskName "PortAtlas"
```

Create `C:\ProgramData\PortAtlas` first. Relative paths of the configuration (`OutputCsvPath`, `DnsCacheFilePath`) are relative to the working directory.

## Windows: service with NSSM

[NSSM](https://nssm.cc) runs a console program as a Windows service. Install it (`winget install NSSM.NSSM`), then in an elevated prompt:

```powershell
nssm install PortAtlas "C:\Program Files\PortAtlas\PortAtlas.exe"
nssm set PortAtlas AppParameters 'audit --no-tui --config "C:\Program Files\PortAtlas\portatlas.json"'
nssm set PortAtlas AppDirectory "C:\ProgramData\PortAtlas"
nssm set PortAtlas AppStdout "C:\ProgramData\PortAtlas\service.log"
nssm set PortAtlas AppStderr "C:\ProgramData\PortAtlas\service_error.log"
nssm set PortAtlas Start SERVICE_AUTO_START
nssm start PortAtlas
```

## Linux: systemd unit

Create `/etc/systemd/system/portatlas.service`:

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

Then:

```bash
sudo mkdir -p /var/lib/portatlas
sudo systemctl daemon-reload
sudo systemctl enable --now portatlas
```

The unit runs as root so that every socket is attributed to its process ([why](../explanation/architecture.md#reading-sockets-instead-of-capturing-packets)).

## Check that it runs

After a few scans, the CSV log (`netflow_audit.csv` in the working directory, or `OutputCsvPath`) contains lines. Without the dashboard, the agent prints its settings at startup and `Graceful shutdown of network flow audit.` when it stops; with NSSM these lines go to `service.log`, with systemd to `journalctl -u portatlas`.

The command must name `audit`: started without a command, PortAtlas 2.0 prints the help and exits with code 0. A unit with `Restart=on-failure` then stays stopped.
