using Avalonia.Threading;
using PortAtlas.Gui.Core.Interaction;

namespace PortAtlas.Gui.Adapters;

/// <summary>
/// Dispatches actions onto the Avalonia UI thread.
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    /// <inheritdoc />
    public void Post(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Dispatcher.UIThread.Post(action);
    }
}
