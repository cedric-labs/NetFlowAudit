> **Language / Langue**: **English** · [Français](gui-architecture.fr.md)

# How the graphical interface is built

The graphical interface lets you fill in a form instead of typing the options of a command. This page explains how it is organized and why it has no description of the options of its own. The decision is recorded in [ADR-0002](../adr/0002-avalonia-gui-generated-from-command-tree.md). The options themselves are in the [CLI reference](../reference/cli.md).

## One source for the options

The options of every command are defined once, in the command tree that the CLI builds for parsing and for the help text. The interface reads that tree and generates one form per command, so a new option appears in the form without any interface code. Only two kinds of information are missing from the tree, and a table keyed by option name supplies them: the section of the form an option belongs to, and whether a path option designates a file to read, a file to write or a directory. A test fails when an option has no entry in that table, or when an entry names an option that no longer exists.

The closed sets of values (for example the layouts of the map) are exposed by the CLI itself as completion values of the option, so the form lists exactly what the CLI accepts. Minimum values are not copied into the form: the CLI validates the command line, and the form shows its message and exit code unchanged.

## Commands in the form

The form offers the commands that run once and stop: `map`, `export rules`, `export ndjson`, `resolve`, `lookup` and `config init`. The `audit` command runs until it is stopped and is not part of the first version of the interface.

## View models and abstractions

Each command descriptor is turned into a `CommandFormViewModel` that groups its option view models (`FlagOptionViewModel`, `TextOptionViewModel`, `IntegerOptionViewModel`, `ChoiceOptionViewModel`, `TextListOptionViewModel`) in the order of `OptionGroup`, omitting empty groups, and updates the command line preview after every change. `MainViewModel` keeps one form instance per command so values typed for a command remain when switching to another command and back. `RunViewModel` manages the execution state (`Idle`, `Running`, `Completed`, `Cancelled`, `Failed`), streams output lines through `IUiDispatcher` and disables the run command while a run is in progress. `ProfilesViewModel` lists, saves and loads named profiles.

The view models live in `PortAtlas.Gui.Core` and reach the operating system and the windowing toolkit only through abstractions: `IPortAtlasRunner` and `IPortAtlasExecutableLocator` for process execution, `IFilePickerService` for file and folder dialogs, `IUiDispatcher` for marshalling output lines to the user interface thread, and `IConfirmationService` for yes / no questions, and `IProfileDirectoryProvider` for locating the profile directory.

## Running a command

The form state, which holds the values typed for one command, is turned into an argument list: the command path, then the options that have a value, then the positional arguments. An option left empty adds nothing, so the CLI applies its default. A repeatable option is written once per value. A preview shows the command line with quotes for display and copy, but the program never runs that text.

The command runs as a child process of the `PortAtlas` executable shipped next to the interface. The process receives the argument array directly, without a shell, so no value can be interpreted as shell syntax. Standard output and standard error are read line by line while the command runs, and cancelling the run ends the process and its children. The interface finds the executable beside itself, then through the `PORTATLAS_EXE` environment variable, and reports both locations when neither holds a file.

Running the executable gives the same behavior as the CLI, including its exit codes, and a failure of a command cannot bring the interface down.

## Profiles

A profile is a named set of form states, one per command, stored as a JSON file in `%APPDATA%\PortAtlas\profiles` on Windows and `$XDG_CONFIG_HOME/portatlas/profiles` (or `~/.config/portatlas/profiles` when `XDG_CONFIG_HOME` is unset) on Linux. A file is written to a temporary name and then renamed over the previous one, so an interruption leaves the earlier profile intact. A profile file that cannot be read raises an error naming the file.

Saving without a typed name updates the selected profile, and overwriting an existing profile, selected or typed by name, always asks for confirmation through `IConfirmationService`. The colour theme and the name of the last profile used are stored in `preferences.json`, one level above the profile directory, and restored when the window opens; a missing or damaged preferences file falls back to the defaults.

A text entry of a repeatable option counts as soon as it is typed, so a value left in the box without pressing Add is still part of the profile and of the command line. The `--input` option accepts files, directories and wildcard patterns, so it offers both a File and a Folder picker.

## Layers

| Project | Role |
| :--- | :--- |
| `PortAtlas.Gui.Core` | Command catalog, option presentation, form state, argument list, runner, profiles, view models and UI abstractions. It has no dependency on the user interface library. |
| `PortAtlas.Gui` | Avalonia views, file picker adapter, UI thread dispatcher and profile directory provider. |
| `PortAtlas.Gui.Tests` | Unit, headless UI and architecture tests. |

The core reaches the process, the file dialogs and the user interface thread only through abstractions, so it is tested without a window.
