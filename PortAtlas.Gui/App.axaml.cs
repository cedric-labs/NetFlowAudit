using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using PortAtlas.Gui.Adapters;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Execution;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;
using PortAtlas.Gui.Views;

namespace PortAtlas.Gui;

/// <summary>
/// Avalonia application entry class.
/// </summary>
public sealed partial class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = new MainWindow();
            var locator = new PortAtlasExecutableLocator(AppContext.BaseDirectory, Environment.GetEnvironmentVariable);
            var runner = new PortAtlasProcessRunner(locator);
            var filePicker = new AvaloniaFilePickerService(() => window.StorageProvider);
            var dispatcher = new AvaloniaUiDispatcher();
            var profileStore = new ProfileStore(new ProfileDirectoryProvider());

            window.DataContext = new MainViewModel(
                CommandCatalog.Build(),
                runner,
                filePicker,
                dispatcher,
                profileStore);

            desktop.MainWindow = window;
        }

        base.OnFrameworkInitializationCompleted();
    }
}
