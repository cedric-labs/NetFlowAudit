using System.CommandLine;
using System.CommandLine.Completions;
using System.CommandLine.Help;
using PortAtlas.Cli.CommandLine;

namespace PortAtlas.Gui.Core.Catalog;

/// <summary>
/// The executable commands of the CLI, read from its command tree so that the GUI never describes an option itself.
/// </summary>
public sealed class CommandCatalog
{
    // audit runs until it is stopped, which the one-shot form of the GUI does not handle (ADR-0002).
    private static readonly string[] ExcludedCommands = ["audit"];

    private CommandCatalog(IReadOnlyList<CommandDescriptor> commands) => Commands = commands;

    /// <summary>
    /// Executable commands (the leaves of the tree) that are not excluded from the GUI.
    /// </summary>
    public IReadOnlyList<CommandDescriptor> Commands { get; }

    /// <summary>
    /// Reads the catalog from the command tree of the CLI.
    /// </summary>
    /// <returns>The catalog of the current CLI.</returns>
    public static CommandCatalog Build() => Build(PortAtlasCommandLine.CreateRootCommand());

    /// <summary>
    /// Reads the catalog from a command tree.
    /// </summary>
    /// <param name="root">Root command of the tree.</param>
    /// <returns>The catalog of the tree.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="root"/> is null.</exception>
    public static CommandCatalog Build(RootCommand root)
    {
        ArgumentNullException.ThrowIfNull(root);

        var commands = new List<CommandDescriptor>();
        foreach (var command in root.Subcommands.Where(c => !ExcludedCommands.Contains(c.Name, StringComparer.Ordinal)))
        {
            CollectLeaves(command, [root], [], commands);
        }

        return new CommandCatalog(commands);
    }

    /// <summary>
    /// Finds a command by its path.
    /// </summary>
    /// <param name="key">Command path joined by spaces, as returned by <see cref="CommandDescriptor.Key"/>.</param>
    /// <returns>The command, or <see langword="null"/> when the catalog has no such command.</returns>
    public CommandDescriptor? Find(string key) =>
        Commands.FirstOrDefault(c => string.Equals(c.Key, key, StringComparison.Ordinal));

    private static void CollectLeaves(Command command, List<Command> ancestors, List<string> parentPath, List<CommandDescriptor> commands)
    {
        var path = new List<string>(parentPath) { command.Name };
        if (command.Subcommands.Count == 0)
        {
            commands.Add(Describe(command, ancestors, path));
            return;
        }

        var lineage = new List<Command>(ancestors) { command };
        foreach (var child in command.Subcommands)
        {
            CollectLeaves(child, lineage, path, commands);
        }
    }

    private static CommandDescriptor Describe(Command command, List<Command> ancestors, List<string> path)
    {
        var options = command.Options
            .Where(IsVisibleOption)
            .Select(option => DescribeOption(option, isGlobal: false))
            .ToList();

        // A recursive option belongs to the command that declares it but is accepted by every command below.
        options.AddRange(ancestors
            .SelectMany(ancestor => ancestor.Options)
            .Where(option => option.Recursive && IsVisibleOption(option))
            .Select(option => DescribeOption(option, isGlobal: true)));

        var arguments = command.Arguments
            .Select(argument => new ArgumentDescriptor(argument.Name, argument.Description ?? string.Empty, argument.Arity.MinimumNumberOfValues > 0))
            .ToList();

        return new CommandDescriptor(path, command.Description ?? string.Empty, options, arguments);
    }

    private static bool IsVisibleOption(Option option) =>
        !option.Hidden && option is not HelpOption and not VersionOption;

    private static OptionDescriptor DescribeOption(Option option, bool isGlobal)
    {
        var kind = ReadKind(option);
        var acceptedValues = kind == OptionValueKind.Choice
            ? option.GetCompletions(CompletionContext.Empty).Select(item => item.Label).ToList()
            : [];

        return new OptionDescriptor(
            option.Name,
            [.. option.Aliases],
            option.Description ?? string.Empty,
            kind,
            acceptedValues,
            option.ValueType.IsArray || option.Arity.MaximumNumberOfValues > 1,
            isGlobal,
            option.Required);
    }

    private static OptionValueKind ReadKind(Option option)
    {
        var valueType = Nullable.GetUnderlyingType(option.ValueType) ?? option.ValueType;
        if (valueType == typeof(bool))
        {
            return OptionValueKind.Flag;
        }

        if (valueType == typeof(int) || valueType == typeof(long))
        {
            return OptionValueKind.Integer;
        }

        if (valueType.IsArray)
        {
            return OptionValueKind.TextList;
        }

        return option.GetCompletions(CompletionContext.Empty).Any() ? OptionValueKind.Choice : OptionValueKind.Text;
    }
}
