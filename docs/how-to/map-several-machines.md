> **Language / Langue**: **English** · [Français](map-several-machines.fr.md)

# Map several machines

Goal: one topology map showing how several servers talk to each other.

## Give each server its own file name

Deploy the same configuration file on every server, with the machine name in the output path:

```json
{
  "OutputCsvPath": "audit_{ServerName}.csv"
}
```

`{ServerName}` becomes the name of each machine, so the collected files do not overwrite each other. `{Date}` can be added to start a new file every day. Run the audit as a service on each server ([how](run-as-a-service.md)).

## Collect the files

Copy the CSV logs of all servers into one folder, for example `collected/`, with any tool you already use (network share, `scp`, Syncthing).

## Render the map

```bash
NetFlowAudit.Cli map -i "collected/audit_*.csv" --html homelab.html
```

`-i` accepts a wildcard pattern, a folder or a file, and can be repeated.

To focus on database traffic:

```bash
NetFlowAudit.Cli map -i collected --include-ports 3306,5432,6379,27017 --html databases.html
```

For an editable diagram, add `--drawio homelab.drawio`. To rearrange a large diagram, select all and apply `Arrange > Layout > Hierarchical` in Draw.io.

## Reproduce a view

The `CLI Command` section of the HTML map shows the `map` command that reproduces the current filters and layout choices. Copy it and add the `-i` option of your audit logs.
