using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Views;

/// <summary>
/// Main window of the PortAtlas GUI application.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes the window.
    /// </summary>
    public MainWindow() => InitializeComponent();

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel || Clipboard is null)
        {
            return;
        }

        await Clipboard.SetTextAsync(viewModel.CurrentForm.Preview).ConfigureAwait(true);
    }

    private void OnThemeClick(object? sender, RoutedEventArgs e)
    {
        if (Application.Current is null)
        {
            return;
        }

        var (next, label) = Application.Current.RequestedThemeVariant switch
        {
            var v when v == ThemeVariant.Light => (ThemeVariant.Dark, "Theme: Dark"),
            var v when v == ThemeVariant.Dark => (ThemeVariant.Default, "Theme: System"),
            _ => (ThemeVariant.Light, "Theme: Light")
        };

        Application.Current.RequestedThemeVariant = next;
        ThemeButton.Content = label;
    }
}
