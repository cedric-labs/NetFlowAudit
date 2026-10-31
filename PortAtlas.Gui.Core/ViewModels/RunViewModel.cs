using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Interaction;

namespace PortAtlas.Gui.Core.ViewModels;

/// <summary>
/// View model that runs the PortAtlas CLI executable, streams its output and supports cancellation.
/// </summary>
/// <param name="runner">Runner that executes the CLI process.</param>
/// <param name="dispatcher">Dispatcher that marshals output lines onto the UI thread.</param>
/// <param name="argumentsProvider">Supplies the argument list to execute when a run starts.</param>
public sealed partial class RunViewModel(
    IPortAtlasRunner runner,
    IUiDispatcher dispatcher,
    Func<IReadOnlyList<string>> argumentsProvider) : ObservableObject
{
    private CancellationTokenSource? _cts;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRunning))]
    [NotifyCanExecuteChangedFor(nameof(RunCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    private RunState _state = RunState.Idle;

    [ObservableProperty]
    private int? _exitCode;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// Whether a CLI process is currently running.
    /// </summary>
    public bool IsRunning => State == RunState.Running;

    /// <summary>
    /// Output lines produced by the running or last completed command.
    /// </summary>
    public ObservableCollection<RunOutputLine> OutputLines { get; } = [];

    private bool CanRun() => State != RunState.Running;

    private bool CanCancel() => State == RunState.Running;

    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync(CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(argumentsProvider);

        OutputLines.Clear();
        ExitCode = null;
        StatusMessage = null;
        State = RunState.Running;

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _cts = linkedCts;

        var progress = new DispatchedProgress(dispatcher, OutputLines.Add);
        try
        {
            var result = await runner.RunAsync(argumentsProvider(), progress, linkedCts.Token).ConfigureAwait(true);
            ExitCode = result.ExitCode;
            State = RunState.Completed;
        }
        catch (OperationCanceledException) when (linkedCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            // User cancellation via CancelCommand transitions the view model state instead of crashing the UI loop.
            StatusMessage = "Cancelled.";
            State = RunState.Cancelled;
        }
        catch (FileNotFoundException ex)
        {
            StatusMessage = ex.Message;
            State = RunState.Failed;
        }
        finally
        {
            _cts = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel() => _cts?.Cancel();

    private sealed class DispatchedProgress(IUiDispatcher dispatcher, Action<RunOutputLine> onLine) : IProgress<RunOutputLine>
    {
        public void Report(RunOutputLine value) => dispatcher.Post(() => onLine(value));
    }
}
