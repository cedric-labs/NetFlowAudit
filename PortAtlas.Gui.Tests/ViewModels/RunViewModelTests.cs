using System.ComponentModel;
using AwesomeAssertions;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class RunViewModelTests
{
    [Fact]
    public async Task Run_WhenRunnerReturnsExitCode_ShowsItUnchanged()
    {
        // Arrange
        var runner = new StubRunner((_, _, _) => Task.FromResult(new RunResult(2)));
        var vm = new RunViewModel(runner, new ImmediateDispatcher(), () => ["lookup", "192.168.1.10"]);

        // Act
        await vm.RunCommand.ExecuteAsync(null);

        // Assert
        vm.State.Should().Be(RunState.Completed);
        vm.ExitCode.Should().Be(2);
    }

    [Fact]
    public async Task Run_WhenRunnerReportsLines_AppendsThemInOrderWithTheirStream()
    {
        // Arrange
        var runner = new StubRunner((_, progress, _) =>
        {
            progress.Report(new RunOutputLine(RunOutputStream.Standard, "first line"));
            progress.Report(new RunOutputLine(RunOutputStream.Error, "second line"));
            return Task.FromResult(new RunResult(0));
        });
        var vm = new RunViewModel(runner, new ImmediateDispatcher(), () => ["map"]);

        // Act
        await vm.RunCommand.ExecuteAsync(null);

        // Assert
        vm.OutputLines.Should().Equal(
            new RunOutputLine(RunOutputStream.Standard, "first line"),
            new RunOutputLine(RunOutputStream.Error, "second line"));
    }

    [Fact]
    public async Task Cancel_WhenRunIsInProgress_EndsInCancelledState()
    {
        // Arrange
        var started = new TaskCompletionSource();
        var runner = new StubRunner(async (_, _, ct) =>
        {
            started.TrySetResult();
            await Task.Delay(Timeout.Infinite, ct);
            return new RunResult(0);
        });
        var vm = new RunViewModel(runner, new ImmediateDispatcher(), () => ["resolve"]);

        // Act
        var runTask = vm.RunCommand.ExecuteAsync(null);
        await Task.WhenAny(started.Task, Task.Delay(500));
        vm.CancelCommand.Execute(null);
        await runTask;

        // Assert
        vm.State.Should().Be(RunState.Cancelled);
    }

    [Fact]
    public async Task Run_WhenExecutableIsMissing_ShowsTheLocatorMessage()
    {
        // Arrange
        var locator = new PortAtlasExecutableLocator("/nonexistent/gui/dir", _ => null);
        var runner = new PortAtlasProcessRunner(locator);
        var vm = new RunViewModel(runner, new ImmediateDispatcher(), () => ["lookup", "192.168.1.1"]);

        // Act
        await vm.RunCommand.ExecuteAsync(null);

        // Assert
        vm.State.Should().Be(RunState.Failed);
        vm.StatusMessage.Should().Contain("/nonexistent/gui/dir")
            .And.Contain(PortAtlasExecutableLocator.EnvironmentVariableName);
    }

    [Fact]
    public async Task Run_WhenProcessCannotStart_FailsWithTheErrorMessage()
    {
        // Arrange
        var runner = new StubRunner((_, _, _) => throw new Win32Exception("Access is denied."));
        var vm = new RunViewModel(runner, new ImmediateDispatcher(), () => ["map"]);

        // Act
        await vm.RunCommand.ExecuteAsync(null);

        // Assert
        vm.State.Should().Be(RunState.Failed);
        vm.StatusMessage.Should().Be("Access is denied.");
    }

    [Fact]
    public async Task RunCommand_WhenRunIsInProgress_CannotExecute()
    {
        // Arrange
        var started = new TaskCompletionSource();
        var release = new TaskCompletionSource();
        var runner = new StubRunner(async (_, _, _) =>
        {
            started.TrySetResult();
            await release.Task;
            return new RunResult(0);
        });
        var vm = new RunViewModel(runner, new ImmediateDispatcher(), () => ["map"]);

        // Act
        var runTask = vm.RunCommand.ExecuteAsync(null);
        await Task.WhenAny(started.Task, Task.Delay(500));
        var canExecuteWhileRunning = vm.RunCommand.CanExecute(null);
        release.SetResult();
        await runTask;

        // Assert
        canExecuteWhileRunning.Should().BeFalse();
        vm.RunCommand.CanExecute(null).Should().BeTrue();
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();
    }

    private sealed class StubRunner(
        Func<IReadOnlyList<string>, IProgress<RunOutputLine>, CancellationToken, Task<RunResult>> handler) : IPortAtlasRunner
    {
        public Task<RunResult> RunAsync(
            IReadOnlyList<string> arguments,
            IProgress<RunOutputLine> output,
            CancellationToken cancellationToken = default) =>
            handler(arguments, output, cancellationToken);
    }
}
