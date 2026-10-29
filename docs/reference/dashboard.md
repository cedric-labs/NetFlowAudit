> **Language / Langue**: **English** · [Français](dashboard.fr.md)

# Terminal dashboard reference

`PortAtlas audit` shows a full-screen dashboard in the terminal, unless `--no-tui` is given, `EnableDashboard` is `false`, or the input or output of the process is redirected.

## Views

| View | Content |
| :--- | :--- |
| Audit Log | Deduplicated flows recorded since startup, including the flows reloaded from the CSV log |
| Active Sockets | Sockets seen by the last scan, before the recording filters are applied |

## Keys

| Key | Action |
| :--- | :--- |
| `Q` | Stop the audit and exit |
| `C` | Redraw the screen |
| `V` or `F` | Switch between the two views |
| `M` | Write the HTML map (`OutputHtmlMapPath`, or `netflow_topology.html`), and the Draw.io diagram when `OutputDrawioPath` is set |
| `↑` `↓` `←` `→` | Move the selection in the table |
| `N` or `Page Down` | Next page |
| `P` or `Page Up` | Previous page |
| `L` or `+` | Load 100 more rows |
| `A` | Load all rows |
| `Enter`, `X` or `E` | Exclude the value of the selected cell |
| `Esc` | Cancel the selection |

`Ctrl+C` and `SIGTERM` stop the audit like `Q`.

## Excluding from the dashboard

Excluding a cell adds its value to the configuration file given by `--config` (or the default one) and applies it at once:

| Selected column | Key updated |
| :--- | :--- |
| Local port | `ExcludedLocalPorts` |
| Remote address | `ExcludedRemoteIps` |
| Remote port | `ExcludedRemotePorts` |
| Process | `ExcludedExactNames` |
| Target | `ExcludedExactNames` |

## Masking for screenshots

`--mask-server`, `--mask-ip` and `--obfuscate` change only what the dashboard displays; the audit log keeps the real values.
