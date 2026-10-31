namespace PortAtlas.Gui.Core.Forms;

/// <summary>
/// Quotes an argument for the command line preview shown to the user.
/// </summary>
internal static class CommandLineQuoting
{
    private static readonly char[] CharactersRequiringQuotes = [' ', '\t', '"'];

    /// <summary>
    /// Wraps the argument in double quotes when it is empty or holds a space, a tab or a quote.
    /// </summary>
    /// <param name="argument">Argument as handed to the executable.</param>
    /// <returns>The argument as it would be typed in a terminal.</returns>
    public static string QuoteForDisplay(string argument)
    {
        if (argument.Length > 0 && argument.IndexOfAny(CharactersRequiringQuotes) < 0)
        {
            return argument;
        }

        return $"\"{argument.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";
    }
}
