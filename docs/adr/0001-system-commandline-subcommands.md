# ADR-0001: Command-line parsing with System.CommandLine subcommands

**Date**: 2026-10-27
**Status**: accepted
**Deciders**: curvecoder (owner)

## Context

Command-line arguments are read by hand in four files (`Program.cs`, `TopologyMapConfigurationBuilder.cs`,
`FirewallRuleExportCommandRunner.cs`, `OfflineResolutionCommandRunner.cs`): 107 read calls, argument
helpers duplicated four times, alias lists copied and already diverging. The program runs one of seven
exclusive modes selected by flag priority, but nothing states which options each mode reads: an option
the selected mode does not read is silently ignored, unknown values silently fall back to defaults, and
the help text and documentation are maintained by hand and contradict the code. No shared `*Forge`
framework covers command-line parsing.

## Decision

We use System.CommandLine 2.0.14 (MIT, native `net8.0` target, no dependency) with one subcommand per
mode: `audit`, `map`, `export rules`, `export ndjson`, `resolve`, `lookup <ip>`, `config init`. The option
definitions are the single source of truth for parsing, `--help` and the generated CLI reference.
This is a breaking change released as 2.0.0; no legacy flag or alias is kept.

Design rules:
- Running without a subcommand prints the help.
- One name per option; short forms only for `-h`, `-i`, `-o`. `--config` is the only global option.
- Shared option families (inputs, flow filters, map rendering, name resolution) are defined once and
  attached to the commands that read them; an option given to a command that does not read it is an error.
- `-i/--input` is repeatable and accepts a file, a directory or a wildcard pattern for `map`,
  `export rules`, `export ndjson` and `resolve` (replaces `--inputs`, `--input-dir`, `--input-filter`).
- Options with a closed set of values reject unknown values and list the accepted ones
  (hidden synonyms such as `arbre`, `force`, `ring` are removed). Values are compared without case.
- `export rules` filters the destination zone with `--zone internal|public|all` (default `all`).
- `--isolated` is removed. Name resolution is controlled by what each option does:
  `audit --no-rdap` (no Internet RDAP query, internal reverse DNS kept), `audit --no-dns`,
  `resolve --cache-only` (no query at all, provided caches only).

## Alternatives Considered

### Alternative 1: In-house declarative option registry feeding the current parser
- **Pros**: no dependency, current syntax kept, no major version.
- **Cons**: per-command validation, help generation and error messages to write and maintain ourselves.
- **Why not**: rebuilds what the standard .NET library provides, and keeps flag-priority mode selection,
  which is the root cause of the "which options go together" question.

### Alternative 2: System.CommandLine with legacy flags kept as deprecated aliases
- **Pros**: existing scripts and services keep working for one release.
- **Cons**: two syntaxes to test and document, aliases are the source of the current drift.
- **Why not**: the owner chose a clean break in 2.0.0.

## Consequences

### Positive
- Each command declares the options it reads; misuse fails with an explicit error.
- `--help` and the CLI reference are generated from the same definitions and checked by tests.
- The four duplicated argument helpers and the double reading of map options disappear.

### Negative
- Every existing script, Windows service (NSSM, scheduled task) and systemd unit must be updated.
- New package dependency to keep up to date.

### Risks
- Behaviour regression during the switch: mitigated by characterization tests written before the
  refactor, then the switch as a `refactor:` commit, then each behaviour change as its own `feat!:`/`fix:` commit.
- `IsolatedMode` and other JSON keys tied to removed options: the configuration file keys are mapped in
  the same change and listed in the 2.0.0 release notes. A file with `"IsolatedMode": true` stops the
  agent with an error asking for `"ResolveIpOwners": false`, because ignoring it would send RDAP queries
  from servers configured to never reach the Internet; `"IsolatedMode": false` only prints a warning.
