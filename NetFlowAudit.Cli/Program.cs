using System.CommandLine;
using NetFlowAudit.Cli.CommandLine;
using NetFlowAudit.Cli.Configuration;
using NetFlowAudit.Cli.Terminal;

namespace NetFlowAudit.Cli;

/// <summary>
/// Main entry point for the NetFlowAudit network flow auditing utility.
/// </summary>
public static class Program
{
    /// <summary>
    /// Parses the command line and runs the selected command.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code of the command; non-zero when the command line is invalid.</returns>
    public static async Task<int> Main(string[] args)
    {
        try
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.InputEncoding = System.Text.Encoding.UTF8;
        }
        catch (IOException)
        {
            // Without an attached terminal (service, redirected streams) the encodings cannot be changed;
            // the process keeps the default ones, which only affects the box-drawing characters of the dashboard.
        }

        ConsoleVirtualTerminalActivator.TryEnable();

        // ProcessTerminationTimeout = null: the audit loop registers its own Ctrl+C and SIGTERM handlers to flush the
        // CSV writers before exiting; System.CommandLine must not force the process exit after its 2-second default.
        // The default exception handler is off so that a configuration error reaches the catch below and is shown as
        // one line instead of a stack trace; any other exception still ends the process with its stack trace.
        var invocation = new InvocationConfiguration { ProcessTerminationTimeout = null, EnableDefaultExceptionHandler = false };
        try
        {
            return await NetFlowAuditCommandLine.CreateRootCommand().Parse(args).InvokeAsync(invocation).ConfigureAwait(false);
        }
        catch (ConfigurationFileException ex)
        {
            await Console.Error.WriteLineAsync(ex.Message).ConfigureAwait(false);
            return 1;
        }
    }
}
