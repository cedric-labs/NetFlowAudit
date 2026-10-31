namespace PortAtlas.Gui.Core.Interaction;

/// <summary>
/// Asks the user to confirm an action before it is carried out.
/// </summary>
public interface IConfirmationService
{
    /// <summary>
    /// Shows <paramref name="message"/> and waits for the user's answer.
    /// </summary>
    /// <param name="message">Question put to the user.</param>
    /// <param name="cancellationToken">Token that cancels the dialog.</param>
    /// <returns><see langword="true"/> when the user accepts, <see langword="false"/> when they decline or close the dialog.</returns>
    Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken = default);
}
