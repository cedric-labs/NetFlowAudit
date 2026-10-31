namespace PortAtlas.Gui.Core.Execution;

/// <summary>
/// Runs a PortAtlas command and streams what it writes.
/// </summary>
public interface IPortAtlasRunner
{
    /// <summary>
    /// Runs the command with the given arguments and reports each output line as soon as it is read.
    /// </summary>
    /// <param name="arguments">Arguments handed to the executable, one per element; never a shell string.</param>
    /// <param name="output">Receives every line of standard output and standard error.</param>
    /// <param name="cancellationToken">Cancelling it terminates the process and its children.</param>
    /// <returns>The exit code once the process has ended.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="arguments"/> or <paramref name="output"/> is null.</exception>
    /// <exception cref="OperationCanceledException">When <paramref name="cancellationToken"/> is cancelled before the process ends.</exception>
    /// <exception cref="FileNotFoundException">When the PortAtlas executable cannot be located.</exception>
    Task<RunResult> RunAsync(IReadOnlyList<string> arguments, IProgress<RunOutputLine> output, CancellationToken cancellationToken = default);
}
