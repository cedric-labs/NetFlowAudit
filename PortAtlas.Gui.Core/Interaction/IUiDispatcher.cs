namespace PortAtlas.Gui.Core.Interaction;

/// <summary>
/// Marshals actions onto the user interface thread.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Schedules <paramref name="action"/> to run on the user interface thread.
    /// </summary>
    /// <param name="action">Action to execute.</param>
    void Post(Action action);
}
