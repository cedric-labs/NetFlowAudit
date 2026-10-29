> **Language / Langue**: **English** · [Français](getting-started.fr.md)

# Getting started

This tutorial runs a first audit on your machine and turns it into a topology map. It takes about ten minutes.

## Download

From the [releases page](https://github.com/curvecoder/PortAtlas/releases), download the archive for your system and extract it into a folder:

- Windows: `PortAtlas-<version>-win-x64.zip`
- Linux: `PortAtlas-<version>-linux-x64.tar.gz`

The executable is self-contained: no .NET runtime is needed. The archive also holds a `portatlas.json` file with the default settings.

## Run a first audit

Open a terminal in that folder, as administrator on Windows.

Windows (PowerShell):

```powershell
.\PortAtlas.exe audit
```

Linux:

```bash
chmod +x ./PortAtlas
sudo ./PortAtlas audit
```

The dashboard opens. Every five seconds it lists the flows found: the program, the remote address and port, and the name resolved for that address. Open a web page or run `git fetch` in another window: new flows appear within a few seconds.

Leave the audit running for a few minutes, then press `Q`. The flows are now in `netflow_audit.csv`, in the same folder.

## Render the map

Windows:

```powershell
.\PortAtlas.exe map --html first_map.html
```

Linux:

```bash
./PortAtlas map --html first_map.html
```

The command prints `Topology map generated: …` with the number of servers and flows. Open `first_map.html` in a browser: your machine sits in the map with the servers it talked to. The page works offline.

## Next steps

- Run the audit permanently: [Run PortAtlas as a service](how-to/run-as-a-service.md).
- Build one map for several servers: [Map several machines](how-to/map-several-machines.md).
- Every command and option: [command-line reference](reference/cli.md).
