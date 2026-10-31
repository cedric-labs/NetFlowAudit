using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Forms;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class ProfilesViewModelTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("portatlas-vm-profiles-").FullName;
    private string _directory => Path.Combine(_root, "profiles");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task Save_WhenNameIsInvalid_ShowsAnErrorAndWritesNothing()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var vm = CreateViewModel(store, new StubConfirmation(true), () => new ProfileDocument(), _ => { });

        // Act
        await vm.SaveCommand.ExecuteAsync("../invalid");

        // Assert
        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        Directory.Exists(_directory).Should().BeFalse();
    }

    [Fact]
    public async Task SaveThenLoad_WhenFormIsFilled_RestoresTheSameState()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var mapDescriptor = CommandCatalog.Build().Find("map") ?? throw new InvalidOperationException("map command missing");
        var form = new CommandFormViewModel(mapDescriptor, new NullFilePicker());
        var vm = CreateViewModel(
            store,
            new StubConfirmation(true),
            () => new ProfileDocument { Forms = { [mapDescriptor.Key] = form.ToState() } },
            doc =>
            {
                if (doc.Forms.TryGetValue(mapDescriptor.Key, out var state))
                {
                    form.LoadState(state);
                }
            });

        form.Options.OfType<ChoiceOptionViewModel>().Single(o => o.Name == "--layout").SelectedValue = "circular";
        form.Options.OfType<FlagOptionViewModel>().Single(o => o.Name == "--listeners-only").IsChecked = true;

        // Act
        await vm.SaveCommand.ExecuteAsync("homelab");
        form.LoadState(new CommandFormState());
        await vm.LoadCommand.ExecuteAsync("homelab");

        // Assert
        vm.Names.Should().Contain("homelab");
        form.Options.OfType<ChoiceOptionViewModel>().Single(o => o.Name == "--layout").SelectedValue.Should().Be("circular");
        form.Options.OfType<FlagOptionViewModel>().Single(o => o.Name == "--listeners-only").IsChecked.Should().BeTrue();
        form.Preview.Should().Be("map --listeners-only --layout circular");
    }

    [Fact]
    public async Task Save_WhenNoNameIsTypedAndAProfileIsSelected_OverwritesItAfterConfirmation()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var confirmation = new StubConfirmation(true);
        var saved = 0;
        var vm = CreateViewModel(store, confirmation, () => { saved++; return new ProfileDocument(); }, _ => { });
        await vm.SaveCommand.ExecuteAsync("homelab");
        vm.ProfileNameInput = string.Empty;
        saved = 0;

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        confirmation.Messages.Should().ContainSingle().Which.Should().Contain("homelab");
        saved.Should().Be(1);
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task Save_WhenUserDeclinesTheOverwrite_KeepsTheStoredProfile()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var first = new ProfileDocument { Forms = { ["map"] = new CommandFormState() } };
        await store.SaveAsync(new ProfileName("homelab"), first);
        var vm = CreateViewModel(store, new StubConfirmation(false), () => new ProfileDocument(), _ => { });

        // Act
        await vm.SaveCommand.ExecuteAsync("homelab");

        // Assert
        var stored = await store.LoadAsync(new ProfileName("homelab"));
        stored.Forms.Should().ContainKey("map");
    }

    [Fact]
    public async Task Save_WhenTypedNameAlreadyExists_AsksForConfirmationBeforeOverwriting()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        await store.SaveAsync(new ProfileName("nas"), new ProfileDocument());
        var confirmation = new StubConfirmation(true);
        var vm = CreateViewModel(store, confirmation, () => new ProfileDocument(), _ => { });
        vm.ProfileNameInput = "nas";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        confirmation.Messages.Should().ContainSingle().Which.Should().Contain("nas");
    }

    [Fact]
    public async Task Save_WhenNameIsNewAndNothingIsSelected_SavesWithoutAskingConfirmation()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var confirmation = new StubConfirmation(true);
        var vm = CreateViewModel(store, confirmation, () => new ProfileDocument(), _ => { });
        vm.ProfileNameInput = "pihole";

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        confirmation.Messages.Should().BeEmpty();
        vm.Names.Should().Contain("pihole");
    }

    [Fact]
    public async Task Save_WhenNoNameAndNoProfileIsSelected_ShowsAnError()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var vm = CreateViewModel(store, new StubConfirmation(true), () => new ProfileDocument(), _ => { });

        // Act
        await vm.SaveCommand.ExecuteAsync(null);

        // Assert
        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Initialize_WhenALastProfileWasUsed_LoadsItAndSelectsIt()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var preferences = new UserPreferencesStore(new FixedDirectory(_directory));
        var first = CreateViewModel(store, new StubConfirmation(true), () => new ProfileDocument(), _ => { });
        await first.SaveCommand.ExecuteAsync("homelab");
        var applied = new List<ProfileDocument>();
        var vm = CreateViewModel(store, new StubConfirmation(true), () => new ProfileDocument(), applied.Add);

        // Act
        await vm.InitializeAsync();

        // Assert
        applied.Should().ContainSingle();
        vm.SelectedProfile.Should().Be("homelab");
        preferences.Load().LastProfile.Should().Be("homelab");
    }

    [Fact]
    public async Task Initialize_WhenTheLastProfileNoLongerExists_LeavesTheFormUntouched()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var preferences = new UserPreferencesStore(new FixedDirectory(_directory));
        await preferences.UpdateAsync(p => p with { LastProfile = "deleted" });
        var applied = new List<ProfileDocument>();
        var vm = CreateViewModel(store, new StubConfirmation(true), () => new ProfileDocument(), applied.Add);

        // Act
        await vm.InitializeAsync();

        // Assert
        applied.Should().BeEmpty();
        vm.ErrorMessage.Should().BeNull();
    }

    private ProfilesViewModel CreateViewModel(
        ProfileStore store,
        IConfirmationService confirmation,
        Func<ProfileDocument> read,
        Action<ProfileDocument> apply) =>
        new(store, new UserPreferencesStore(new FixedDirectory(_directory)), confirmation, read, apply);

    private sealed class StubConfirmation(bool answer) : IConfirmationService
    {
        public List<string> Messages { get; } = [];

        public Task<bool> ConfirmAsync(string message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.FromResult(answer);
        }
    }

    private sealed class FixedDirectory(string path) : IProfileDirectoryProvider
    {
        public string GetProfileDirectory() => path;
    }

    private sealed class NullFilePicker : IFilePickerService
    {
        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
