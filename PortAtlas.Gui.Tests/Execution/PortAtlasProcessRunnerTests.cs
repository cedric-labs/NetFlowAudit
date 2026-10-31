using AwesomeAssertions;
using PortAtlas.Gui.Core.Execution;

namespace PortAtlas.Gui.Tests.Execution;

public sealed class PortAtlasProcessRunnerTests
{
    private static readonly string ShellPath = OperatingSystem.IsWindows() ? "cmd.exe" : "sh";

    private static PortAtlasProcessRunner ShellRunner() => new(new FixedExecutableLocator(ShellPath));

    private static PortAtlasProcessRunner PortAtlasRunner() =>
        new(new PortAtlasExecutableLocator(AppContext.BaseDirectory, _ => null));

    private static string[] ShellCommand(string windows, string unix) =>
        OperatingSystem.IsWindows() ? ["/c", windows] : ["-c", unix];

    [Fact]
    public async Task RunAsync_WhenProcessWritesToStdoutAndStderr_StreamsBothLines()
    {
        // Arrange
        var lines = new CollectedOutput();

        // Act
        var result = await ShellRunner().RunAsync(
            ShellCommand("echo out& echo err>&2", "echo out; echo err >&2"), lines, CancellationToken.None);

        // Assert
        result.ExitCode.Should().Be(0);
        lines.Lines.Should().Contain(new RunOutputLine(RunOutputStream.Standard, "out"));
        lines.Lines.Should().Contain(new RunOutputLine(RunOutputStream.Error, "err"));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_TerminatesProcessAndThrowsOperationCanceledException()
    {
        // Arrange
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
        var started = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var act = () => ShellRunner().RunAsync(
            ShellCommand("ping -n 60 127.0.0.1 >nul", "sleep 60"), new CollectedOutput(), cancellation.Token);

        // Assert
        await act.Should().ThrowAsync<OperationCanceledException>();
        started.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task RunAsync_WhenCommandFails_ReturnsExitCodeOne()
    {
        // Arrange
        var lines = new CollectedOutput();

        // Act
        var result = await PortAtlasRunner().RunAsync(["map", "--no-such-option"], lines, CancellationToken.None);

        // Assert
        result.ExitCode.Should().Be(1);
        lines.Lines.Should().Contain(line => line.Stream == RunOutputStream.Error);
    }

    private sealed class FixedExecutableLocator(string path) : IPortAtlasExecutableLocator
    {
        public string Locate() => path;
    }

    private sealed class CollectedOutput : IProgress<RunOutputLine>
    {
        private readonly List<RunOutputLine> _lines = [];

        public IReadOnlyList<RunOutputLine> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        // Progress<T> would post to the thread pool and the lines could arrive after RunAsync returns.
        public void Report(RunOutputLine value)
        {
            lock (_lines)
            {
                _lines.Add(value);
            }
        }
    }
}
