using AwesomeAssertions;
using PortAtlas.Gui.Core.Forms;
using PortAtlas.Gui.Core.Profiles;

namespace PortAtlas.Gui.Tests.Profiles;

public sealed class ProfileStoreTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("portatlas-profiles-").FullName;
    private readonly ProfileStore _store;

    public ProfileStoreTests() => _store = new ProfileStore(new FixedProfileDirectory(_directory));

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static ProfileDocument DocumentWithLayout(string layout)
    {
        var form = new CommandFormState();
        form.SetValue("--layout", layout);
        form.SetList("--input", ["nas.csv", "proxmox.csv"]);
        form.SetFlag("--listeners-only", true);
        return new ProfileDocument { Forms = { ["map"] = form } };
    }

    [Fact]
    public async Task Save_WhenProfileExists_ReplacesItWithoutLeavingTemporaryFile()
    {
        // Arrange
        var name = new ProfileName("homelab");
        await _store.SaveAsync(name, DocumentWithLayout("tree"), CancellationToken.None);

        // Act
        await _store.SaveAsync(name, DocumentWithLayout("grid"), CancellationToken.None);
        var loaded = await _store.LoadAsync(name, CancellationToken.None);

        // Assert
        Directory.GetFiles(_directory).Select(Path.GetFileName).Should().Equal("homelab.json");
        loaded.Forms["map"].Values["--layout"].Should().Be("grid");
        loaded.Forms["map"].Lists["--input"].Should().Equal("nas.csv", "proxmox.csv");
        loaded.Forms["map"].Flags["--listeners-only"].Should().BeTrue();
    }

    [Fact]
    public async Task Load_WhenFileIsTruncated_ThrowsProfileFileException()
    {
        // Arrange
        var path = Path.Combine(_directory, "broken.json");
        await File.WriteAllTextAsync(path, "{\"Forms\": {\"map\": {\"Flags\": {", CancellationToken.None);

        // Act
        var act = () => _store.LoadAsync(new ProfileName("broken"), CancellationToken.None);

        // Assert
        (await act.Should().ThrowAsync<ProfileFileException>())
            .Which.FilePath.Should().Be(path);
    }

    [Theory]
    [InlineData("")]
    [InlineData("../escape")]
    [InlineData(" padded")]
    public void Constructor_WhenNameIsNotAValidFileName_ThrowsArgumentException(string value)
    {
        // Act
        var act = () => new ProfileName(value);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    private sealed class FixedProfileDirectory(string directory) : IProfileDirectoryProvider
    {
        public string GetProfileDirectory() => directory;
    }
}
