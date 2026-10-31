using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Forms;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.Profiles;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class ProfilesViewModelTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("portatlas-vm-profiles-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Save_WhenNameIsInvalid_ShowsAnErrorAndWritesNothing()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var vm = new ProfilesViewModel(store, () => new ProfileDocument(), _ => { });

        // Act
        await vm.SaveCommand.ExecuteAsync("../invalid");

        // Assert
        vm.ErrorMessage.Should().NotBeNullOrWhiteSpace();
        Directory.GetFiles(_directory).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveThenLoad_WhenFormIsFilled_RestoresTheSameState()
    {
        // Arrange
        var store = new ProfileStore(new FixedDirectory(_directory));
        var mapDescriptor = CommandCatalog.Build().Find("map") ?? throw new InvalidOperationException("map command missing");
        var form = new CommandFormViewModel(mapDescriptor, new NullFilePicker());
        var vm = new ProfilesViewModel(
            store,
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
