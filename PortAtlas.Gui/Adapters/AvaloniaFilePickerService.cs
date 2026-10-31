using Avalonia.Platform.Storage;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Interaction;

namespace PortAtlas.Gui.Adapters;

/// <summary>
/// Adapts Avalonia's <see cref="IStorageProvider"/> to <see cref="IFilePickerService"/>.
/// </summary>
/// <param name="storageProviderAccessor">Returns the active window's storage provider when available.</param>
public sealed class AvaloniaFilePickerService(Func<IStorageProvider?> storageProviderAccessor) : IFilePickerService
{
    /// <inheritdoc />
    public async Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var provider = storageProviderAccessor();
        if (provider is null)
        {
            return null;
        }

        return kind switch
        {
            PathKind.FileToRead or PathKind.FileOrDirectoryToRead => await PickFileToReadAsync(provider).ConfigureAwait(true),
            PathKind.FileToWrite => await PickFileToWriteAsync(provider).ConfigureAwait(true),
            PathKind.Directory => await PickDirectoryAsync(provider).ConfigureAwait(true),
            _ => null
        };
    }

    private static async Task<string?> PickFileToReadAsync(IStorageProvider provider)
    {
        var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false }).ConfigureAwait(true);
        return files.Count > 0 ? files[0].TryGetLocalPath() ?? files[0].Name : null;
    }

    private static async Task<string?> PickFileToWriteAsync(IStorageProvider provider)
    {
        var file = await provider.SaveFilePickerAsync(new FilePickerSaveOptions()).ConfigureAwait(true);
        return file is null ? null : file.TryGetLocalPath() ?? file.Name;
    }

    private static async Task<string?> PickDirectoryAsync(IStorageProvider provider)
    {
        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false }).ConfigureAwait(true);
        return folders.Count > 0 ? folders[0].TryGetLocalPath() ?? folders[0].Name : null;
    }
}
