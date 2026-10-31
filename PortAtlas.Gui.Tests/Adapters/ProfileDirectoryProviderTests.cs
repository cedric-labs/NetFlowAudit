using AwesomeAssertions;
using PortAtlas.Gui.Adapters;

namespace PortAtlas.Gui.Tests.Adapters;

public sealed class ProfileDirectoryProviderTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("portatlas-provider-").FullName;
    private string ApplicationDirectory => Path.Combine(_root, "app");
    private string LegacyDirectory => Path.Combine(_root, "legacy");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void GetProfileDirectory_WhenNothingExists_ReturnsProfilesFolderNextToTheApplication()
    {
        // Arrange
        var provider = new ProfileDirectoryProvider(ApplicationDirectory, LegacyDirectory);

        // Act
        var directory = provider.GetProfileDirectory();

        // Assert
        directory.Should().Be(Path.Combine(ApplicationDirectory, "Data", "profiles"));
        Directory.Exists(directory).Should().BeTrue();
    }

    [Fact]
    public void GetProfileDirectory_WhenLegacyProfilesExist_CopiesProfilesAndPreferences()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(LegacyDirectory, "profiles"));
        File.WriteAllText(Path.Combine(LegacyDirectory, "profiles", "home.json"), "{}");
        File.WriteAllText(Path.Combine(LegacyDirectory, "preferences.json"), "{\"theme\":1}");
        var provider = new ProfileDirectoryProvider(ApplicationDirectory, LegacyDirectory);

        // Act
        var directory = provider.GetProfileDirectory();

        // Assert
        File.ReadAllText(Path.Combine(directory, "home.json")).Should().Be("{}");
        File.ReadAllText(Path.Combine(directory, "..", "preferences.json")).Should().Be("{\"theme\":1}");
        File.Exists(Path.Combine(LegacyDirectory, "profiles", "home.json")).Should().BeTrue();
    }

    [Fact]
    public void GetProfileDirectory_WhenNewProfilesAlreadyExist_DoesNotOverwriteThemWithLegacyOnes()
    {
        // Arrange
        Directory.CreateDirectory(Path.Combine(LegacyDirectory, "profiles"));
        File.WriteAllText(Path.Combine(LegacyDirectory, "profiles", "home.json"), "old");
        var newProfiles = Path.Combine(ApplicationDirectory, "Data", "profiles");
        Directory.CreateDirectory(newProfiles);
        File.WriteAllText(Path.Combine(newProfiles, "home.json"), "new");
        var provider = new ProfileDirectoryProvider(ApplicationDirectory, LegacyDirectory);

        // Act
        var directory = provider.GetProfileDirectory();

        // Assert
        File.ReadAllText(Path.Combine(directory, "home.json")).Should().Be("new");
    }

    [Fact]
    public void GetProfileDirectory_WhenApplicationFolderIsNotWritable_FallsBackToTheLegacyFolder()
    {
        // Arrange: a file where the Data folder should be makes the folder impossible to create.
        Directory.CreateDirectory(ApplicationDirectory);
        File.WriteAllText(Path.Combine(ApplicationDirectory, "Data"), "blocker");
        var provider = new ProfileDirectoryProvider(ApplicationDirectory, LegacyDirectory);

        // Act
        var directory = provider.GetProfileDirectory();

        // Assert
        directory.Should().Be(Path.Combine(LegacyDirectory, "profiles"));
    }
}
