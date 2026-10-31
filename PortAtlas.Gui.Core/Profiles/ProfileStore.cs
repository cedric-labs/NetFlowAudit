using System.Text.Json;

namespace PortAtlas.Gui.Core.Profiles;

/// <summary>
/// Reads and writes named profiles as JSON files, one file per profile.
/// </summary>
/// <param name="directoryProvider">Supplies the directory of the profiles.</param>
public sealed class ProfileStore(IProfileDirectoryProvider directoryProvider)
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    /// <summary>
    /// Writes a profile, replacing the previous one only once the new file is complete.
    /// </summary>
    /// <param name="name">Name of the profile.</param>
    /// <param name="document">Content to store.</param>
    /// <param name="cancellationToken">Token that cancels the write.</param>
    /// <returns>A task that ends when the profile is stored.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="document"/> is null.</exception>
    public async Task SaveAsync(ProfileName name, ProfileDocument document, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        var directory = directoryProvider.GetProfileDirectory();
        Directory.CreateDirectory(directory);

        var target = PathOf(directory, name);
        // A crash during the write must leave the previous profile intact, hence the temporary file and the rename.
        var temporary = $"{target}.tmp";
        var stream = File.Create(temporary);
        await using (stream.ConfigureAwait(false))
        {
            await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(temporary, target, overwrite: true);
    }

    /// <summary>
    /// Reads a profile.
    /// </summary>
    /// <param name="name">Name of the profile.</param>
    /// <param name="cancellationToken">Token that cancels the read.</param>
    /// <returns>The stored content.</returns>
    /// <exception cref="FileNotFoundException">When no profile has this name.</exception>
    /// <exception cref="ProfileFileException">When the file is not a valid profile; the exception names the file.</exception>
    public async Task<ProfileDocument> LoadAsync(ProfileName name, CancellationToken cancellationToken = default)
    {
        var path = PathOf(directoryProvider.GetProfileDirectory(), name);
        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                var document = await JsonSerializer.DeserializeAsync<ProfileDocument>(stream, SerializerOptions, cancellationToken).ConfigureAwait(false);
                return document ?? throw ProfileFileException.ForFile(path, new JsonException("The file holds a null document."));
            }
            catch (JsonException ex)
            {
                throw ProfileFileException.ForFile(path, ex);
            }
        }
    }

    /// <summary>
    /// Lists the names of all stored profiles, sorted alphabetically.
    /// </summary>
    /// <returns>The names of the existing profile files.</returns>
    public IReadOnlyList<string> ListNames()
    {
        var directory = directoryProvider.GetProfileDirectory();
        if (!Directory.Exists(directory))
        {
            return [];
        }

        return [.. Directory.EnumerateFiles(directory, "*.json")
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.OrdinalIgnoreCase)];
    }

    private static string PathOf(string directory, ProfileName name) => Path.Combine(directory, $"{name.Value}.json");
}
