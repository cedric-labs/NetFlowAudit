namespace PortAtlas.Gui.Core.Forms;

/// <summary>
/// Values typed in the form of one command, keyed by option long name. It knows nothing of the user interface.
/// </summary>
public sealed class CommandFormState
{
    /// <summary>
    /// Flag options and whether they are switched on.
    /// </summary>
    public Dictionary<string, bool> Flags { get; init; } = [];

    /// <summary>
    /// Single-value options (text, integer, choice, path) and their typed value.
    /// </summary>
    public Dictionary<string, string> Values { get; init; } = [];

    /// <summary>
    /// Repeatable options and their values, in the order typed.
    /// </summary>
    public Dictionary<string, List<string>> Lists { get; init; } = [];

    /// <summary>
    /// Positional arguments keyed by argument name.
    /// </summary>
    public Dictionary<string, string> Arguments { get; init; } = [];

    /// <summary>
    /// Switches a flag option on or off.
    /// </summary>
    /// <param name="optionName">Long name of the option.</param>
    /// <param name="isOn">Whether the flag is emitted.</param>
    public void SetFlag(string optionName, bool isOn) => Flags[optionName] = isOn;

    /// <summary>
    /// Sets the value of a single-value option; a blank value clears it so that the CLI applies its default.
    /// </summary>
    /// <param name="optionName">Long name of the option.</param>
    /// <param name="value">Typed value.</param>
    public void SetValue(string optionName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Values.Remove(optionName);
            return;
        }

        Values[optionName] = value;
    }

    /// <summary>
    /// Sets the values of a repeatable option; an empty sequence clears it.
    /// </summary>
    /// <param name="optionName">Long name of the option.</param>
    /// <param name="values">Typed values, blank ones are dropped.</param>
    public void SetList(string optionName, IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        var kept = values.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
        if (kept.Count == 0)
        {
            Lists.Remove(optionName);
            return;
        }

        Lists[optionName] = kept;
    }

    /// <summary>
    /// Sets a positional argument; a blank value clears it.
    /// </summary>
    /// <param name="argumentName">Name of the argument.</param>
    /// <param name="value">Typed value.</param>
    public void SetArgument(string argumentName, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Arguments.Remove(argumentName);
            return;
        }

        Arguments[argumentName] = value;
    }
}
