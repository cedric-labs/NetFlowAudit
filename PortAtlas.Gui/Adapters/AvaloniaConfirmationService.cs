using Avalonia.Controls;
using Avalonia.Layout;
using PortAtlas.Gui.Core.Interaction;

namespace PortAtlas.Gui.Adapters;

/// <summary>
/// Asks for confirmation in a modal Yes / No dialog owned by the main window.
/// </summary>
/// <param name="ownerAccessor">Returns the window that owns the dialog, when it is available.</param>
public sealed class AvaloniaConfirmationService(Func<Window?> ownerAccessor) : IConfirmationService
{
    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = ownerAccessor();
        if (owner is null)
        {
            // Without a window there is nobody to ask, and overwriting a profile silently is the worse outcome.
            return false;
        }

        var dialog = BuildDialog(message);
        return await dialog.ShowDialog<bool>(owner).ConfigureAwait(true);
    }

    private static Window BuildDialog(string message)
    {
        var yes = new Button { Content = "Yes", IsDefault = true, MinWidth = 80 };
        var no = new Button { Content = "No", IsCancel = true, MinWidth = 80 };
        var dialog = new Window
        {
            Title = "PortAtlas",
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 320,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
            Content = new StackPanel
            {
                Margin = new Avalonia.Thickness(20),
                Spacing = 16,
                Children =
                {
                    new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MaxWidth = 420 },
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Spacing = 8,
                        Children = { yes, no }
                    }
                }
            }
        };

        yes.Click += (_, _) => dialog.Close(true);
        no.Click += (_, _) => dialog.Close(false);
        return dialog;
    }
}
