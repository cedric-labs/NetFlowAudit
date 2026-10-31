using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Views;

/// <summary>
/// Main window of the PortAtlas GUI application.
/// </summary>
public sealed partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    /// <summary>
    /// Initializes the window.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private async void OnCopyClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null || Clipboard is null)
        {
            return;
        }

        await Clipboard.SetTextAsync(_viewModel.CurrentForm.Preview).ConfigureAwait(true);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;
        if (_viewModel is null)
        {
            return;
        }

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        ApplyTheme(_viewModel.Theme);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Theme) && _viewModel is not null)
        {
            ApplyTheme(_viewModel.Theme);
        }
    }

    private static void ApplyTheme(ThemeChoice theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = theme switch
        {
            ThemeChoice.Light => ThemeVariant.Light,
            ThemeChoice.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default
        };
    }
}
