using PortAtlas.Gui.Core.Catalog;

namespace PortAtlas.Gui.Core.Forms;

/// <summary>
/// Turns the values typed in a form into the arguments of the command line.
/// </summary>
public static class ArgumentListBuilder
{
    /// <summary>
    /// Builds the argument list: command path, then the options that have a value, then the positional arguments.
    /// An option left empty emits nothing, so that the CLI applies its default.
    /// </summary>
    /// <param name="command">Command the form belongs to.</param>
    /// <param name="state">Values typed in the form.</param>
    /// <returns>The argument array and its preview.</returns>
    /// <exception cref="ArgumentNullException">When an argument is null.</exception>
    public static BuiltCommandLine Build(CommandDescriptor command, CommandFormState state)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(state);

        var arguments = new List<string>(command.Path);
        foreach (var option in command.Options)
        {
            AppendOption(arguments, option, state);
        }

        foreach (var argument in command.Arguments)
        {
            if (state.Arguments.TryGetValue(argument.Name, out var value))
            {
                arguments.Add(value);
            }
        }

        return new BuiltCommandLine(arguments, string.Join(' ', arguments.Select(CommandLineQuoting.QuoteForDisplay)));
    }

    private static void AppendOption(List<string> arguments, OptionDescriptor option, CommandFormState state)
    {
        switch (option.Kind)
        {
            case OptionValueKind.Flag when state.Flags.TryGetValue(option.Name, out var isOn) && isOn:
                arguments.Add(option.Name);
                break;
            case OptionValueKind.TextList when state.Lists.TryGetValue(option.Name, out var values):
                foreach (var value in values)
                {
                    arguments.Add(option.Name);
                    arguments.Add(value);
                }

                break;
            case OptionValueKind.Text or OptionValueKind.Integer or OptionValueKind.Choice when state.Values.TryGetValue(option.Name, out var single):
                arguments.Add(option.Name);
                arguments.Add(single);
                break;
            default:
                break;
        }
    }
}
