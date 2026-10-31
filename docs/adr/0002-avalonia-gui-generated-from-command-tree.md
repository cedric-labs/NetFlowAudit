# ADR-0002: Cross-platform desktop GUI with Avalonia, generated from the command tree

**Date**: 2026-10-31
**Status**: accepted
**Deciders**: curvecoder (owner)

## Context

The one-shot operations of PortAtlas (`map`, `export rules`, `export ndjson`, `resolve`, `lookup`, `config init`) take
many options, most of them shared between commands through option families (inputs, flow filters, map rendering).
A graphical front end must expose all of them and run on Windows and Linux, the two platforms PortAtlas already
ships binaries for. Windows Forms runs on Windows only. The option definitions already live in one place, the
System.CommandLine tree built by `PortAtlasCommandLine.CreateRootCommand()` (ADR-0001), and a second hand-written
description of the options would drift from it.

## Decision

We build the GUI with Avalonia 11 (MIT) in the same solution as the CLI, versioned by the same MinVer tags and
shipped next to the `PortAtlas` executable. Forms are generated from the command tree, and a command is run as a
child process of the shipped executable with an argument list. `audit`, which runs until stopped, is out of the
first version.

Structure:
- `PortAtlas.Gui.Core` (`net8.0`, no UI dependency): command catalog read from `CreateRootCommand()`, option
  presentation table, form state, argument list builder, profile store, runner abstraction, view models
  (CommunityToolkit.Mvvm). It references `PortAtlas.Cli` by project reference.
- `PortAtlas.Gui` (Avalonia): views, file picker adapter, process runner, executable locator.
- `PortAtlas.Gui.Tests`: xUnit, AwesomeAssertions, NetArchTest, Avalonia.Headless.

Design rules:
- The catalog is the only source of options. A presentation table keyed by option name adds what the tree does not
  say (group, path widget kind); an architecture test fails when an option has no entry or an entry names no option.
- Closed value sets are exposed by the CLI itself (completion sources of the option), not copied into the GUI.
- The GUI does not duplicate validation such as minimum values: the CLI is the authority, its message and exit code
  are shown as they are.
- The child process receives an argument list, never a shell string.
- `Gui.Core` reaches the process, the file dialogs and the clock only through abstractions
  (`IPortAtlasRunner`, `IFilePickerService`, `TimeProvider`).

## Alternatives Considered

### Alternative 1: Windows Forms
- **Pros**: native on Windows, familiar designer.
- **Cons**: Windows only.
- **Why not**: PortAtlas targets Windows and Linux.

### Alternative 2: Eto.Forms
- **Pros**: code API close to Windows Forms, native widgets on each platform.
- **Cons**: smaller ecosystem, weaker data grid and theming support.
- **Why not**: the form is mostly lists, chips and path pickers, where Avalonia is better served.

### Alternative 3: Hand-written screens per command
- **Pros**: full control of each layout.
- **Cons**: every new CLI option needs GUI work, and the two drift apart.
- **Why not**: the command tree already describes the options.

### Alternative 4: Call the command runners in the GUI process
- **Pros**: no child process.
- **Cons**: the runners write to `Console` (allowed in the CLI layer), cancellation and exit codes differ from the
  CLI, a crash takes the GUI down.
- **Why not**: a child process gives exact parity with the CLI, streamed output and cancellation by termination.

## Consequences

### Positive
- A new CLI option appears in the GUI without GUI code, once it has a presentation entry.
- The GUI runs exactly what the CLI runs, so the documented behavior is the only behavior.
- The generated command line can be shown and copied.

### Negative
- A second UI stack (Avalonia, MVVM) to maintain and to publish per platform.
- The GUI needs the `PortAtlas` executable next to it.

### Risks
- System.CommandLine does not expose some metadata (family of an option, minimum of an integer): the presentation
  table and the architecture test cover the first, the CLI error message covers the second.
- Avalonia headless tests do not cover native file dialogs: the picker adapter stays thin and is checked by hand.
