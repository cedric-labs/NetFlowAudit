> **Language / Langue**: **English** · [Français](upgrade-to-3.0.fr.md)

# Upgrade from 2.x to 3.0

PortAtlas 3.0 is the new name of NetFlowAudit. The functional behavior remains identical, but the executable name, the default configuration file name and the repository URL have changed. Update scripts, scheduled tasks, services and units before deploying 3.0.

## Executable name

The executable is now named `PortAtlas` instead of `NetFlowAudit.Cli`:

| Operating System | 2.x | 3.0 |
| :--- | :--- | :--- |
| Windows | `NetFlowAudit.Cli.exe` | `PortAtlas.exe` |
| Linux | `NetFlowAudit.Cli` | `PortAtlas` |

Update your PATH, service definitions (systemd unit files, Windows service or Scheduled Tasks) and automated scripts to point to `PortAtlas.exe` (or `PortAtlas`).

## Configuration file

The default configuration file name is now `portatlas.json` instead of `netflowaudit.json`:

- Rename your existing `netflowaudit.json` to `portatlas.json`.
- There is no automatic fallback: PortAtlas does not look for `netflowaudit.json`. If only `netflowaudit.json` is present and no `--config` option is specified, PortAtlas runs with built-in default settings.
- If you generate a new configuration template with `config init`, it creates `portatlas.json`.

## Repository URL

The project repository has moved:

- Old: `https://github.com/curvecoder/NetFlowAudit`
- New: `https://github.com/curvecoder/PortAtlas`

Update your git remotes, documentation bookmarks and download links accordingly.

## HTML maps

Maps generated with 2.x proposed a command starting with `NetFlowAudit.Cli map` in their copy box. Regenerating maps with 3.0 generates the `PortAtlas map` command syntax.
