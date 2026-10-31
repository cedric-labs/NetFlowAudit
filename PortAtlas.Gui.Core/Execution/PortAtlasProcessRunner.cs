using System.Diagnostics;
using System.Text;

namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Runs the PortAtlas executable as a child process, without a shell.
/// </summary>
/// <param name="locator">Finds the executable.</param>
public sealed class PortAtlasProcessRunner(IPortAtlasExecutableLocator locator) : IPortAtlasRunner
{
    /// <inheritdoc />
    public async Task<RunResult> RunAsync(IReadOnlyList<string> arguments, IProgress<RunOutputLine> output, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(output);

        var startInfo = new ProcessStartInfo(locator.Locate())
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        var readers = Task.WhenAll(
            ForwardLinesAsync(process.StandardOutput, RunOutputStream.Standard, output),
            ForwardLinesAsync(process.StandardError, RunOutputStream.Error, output));

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Killing the tree is what stops the command: the runner itself has nothing else to cancel.
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await readers.ConfigureAwait(false);
            throw;
        }

        await readers.ConfigureAwait(false);
        return new RunResult(process.ExitCode);
    }

    private static async Task ForwardLinesAsync(StreamReader reader, RunOutputStream stream, IProgress<RunOutputLine> output)
    {
        while (await reader.ReadLineAsync().ConfigureAwait(false) is { } line)
        {
            output.Report(new RunOutputLine(stream, line));
        }
    }
}
