> **Language / Langue**: **English** · [Français](README.fr.md)

# NetFlowAudit

<p align="center">
  <img src="assets/netflowaudit-icon.png" alt="NetFlowAudit logo" width="200" />
  <br/>
  <a href="https://ko-fi.com/curvecoder"><img src="https://ko-fi.com/img/githubbutton_sm.svg" alt="Support curvecoder on Ko-fi" /></a>
</p>

NetFlowAudit records which services a Windows or Linux machine talks to, over days or weeks, and which program is responsible. It reads the socket tables of the operating system at a fixed interval, attributes each socket to its process, and keeps one line per flow in a CSV log. From those logs it draws offline HTML topology maps, Draw.io diagrams and firewall rule matrices.

> [!NOTE]
> NetFlowAudit started as a way to see what really ran and talked on a personal setup (Proxmox servers, a Windows PC, a Linux desktop). It is shared in the hope that it helps someone else.

## Why

Preparing a migration, writing firewall rules or reviewing a server raises the same question: what does this machine connect to, and what listens on it? Packet capture needs a driver and produces large files without the process behind each connection; `netstat` shows one instant and misses scheduled jobs; logging every connection floods the disk. NetFlowAudit runs in the background, without driver, and keeps a deduplicated history with the program, its command line and the application it launched (`java -jar`, `python`, `powershell`…).

<p align="center">
  <img src="assets/tui-dashboard-preview.png" alt="NetFlowAudit terminal dashboard" width="100%" />
</p>

<p align="center">
  <img src="assets/topology-map-preview.png" alt="NetFlowAudit HTML topology map" width="100%" />
</p>

## Install

Download the archive for your system from the [releases page](https://github.com/curvecoder/NetFlowAudit/releases) (`NetFlowAudit-<version>-win-x64.zip` or `NetFlowAudit-<version>-linux-x64.tar.gz`) and extract it. The executable includes the .NET runtime.

To build from source (.NET 8 SDK):

```bash
dotnet publish NetFlowAudit.Cli/NetFlowAudit.Cli.csproj -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -o publish/linux-x64
```

Use `-r win-x64` for Windows.

## Use

Audit the machine with the terminal dashboard (as administrator on Windows, with `sudo` on Linux):

```bash
NetFlowAudit.Cli audit
```

Draw a map from the audit logs of several servers:

```bash
NetFlowAudit.Cli map -i "collected/audit_*.csv" --html fleet.html
```

Export the internal flows seen at least five times as a firewall rule matrix:

```bash
NetFlowAudit.Cli export rules -i collected -o rules.csv --zone internal --min-observations 5
```

## Documentation

| | |
| :--- | :--- |
| Tutorial | [Getting started](docs/getting-started.md) |
| How-to guides | [Run as a service](docs/how-to/run-as-a-service.md) · [Map a server fleet](docs/how-to/map-a-server-fleet.md) · [Export firewall rules](docs/how-to/export-firewall-rules.md) · [Audit isolated servers](docs/how-to/audit-isolated-servers.md) · [Diagnose a missing host name](docs/how-to/diagnose-name-resolution.md) · [Upgrade from 1.x](docs/how-to/upgrade-to-2.0.md) |
| Reference | [Command line](docs/reference/cli.md) · [Configuration file](docs/reference/configuration.md) · [Output formats](docs/reference/output-formats.md) · [Terminal dashboard](docs/reference/dashboard.md) |
| Explanation | [How NetFlowAudit works](docs/explanation/architecture.md) · [Architecture decisions](docs/adr/README.md) |

## Development

`./verify.ps1` runs formatting, the Release build with warnings as errors, the tests with coverage and the SonarQube analysis.

## Support

If NetFlowAudit helps you, you can support its maintenance on [Ko-fi](https://ko-fi.com/curvecoder).

## License

[MIT](LICENSE)
