using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.ViewModels;

public sealed class OptionViewModelTests
{
    [Fact]
    public async Task Browse_WhenPickerReturnsPath_SetsTheValue()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--html", [], "HTML output", OptionValueKind.Text, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Outputs, PathKind.FileToWrite);
        var picker = new StubFilePickerService("/tmp/homelab-map.html");
        var vm = new TextOptionViewModel(descriptor, presentation, picker, () => { });

        // Act
        await vm.BrowseCommand.ExecuteAsync(null);

        // Assert
        vm.Value.Should().Be("/tmp/homelab-map.html");
    }

    [Fact]
    public async Task Browse_WhenPickerReturnsNull_KeepsThePreviousValue()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--html", [], "HTML output", OptionValueKind.Text, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Outputs, PathKind.FileToWrite);
        var picker = new StubFilePickerService(null);
        var vm = new TextOptionViewModel(descriptor, presentation, picker, () => { })
        {
            Value = "existing.html"
        };

        // Act
        await vm.BrowseCommand.ExecuteAsync(null);

        // Assert
        vm.Value.Should().Be("existing.html");
    }

    [Fact]
    public async Task BrowseDirectory_WhenOptionReadsFileOrDirectory_AddsTheFolderToTheList()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--input", [], "Audit logs", OptionValueKind.TextList, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Inputs, PathKind.FileOrDirectoryToRead);
        var picker = new RecordingFilePickerService("/srv/audits");
        var vm = new TextListOptionViewModel(descriptor, presentation, picker, () => { });

        // Act
        await vm.BrowseDirectoryCommand.ExecuteAsync(null);

        // Assert
        vm.Values.Should().Equal("/srv/audits");
        picker.Kinds.Should().Equal(PathKind.Directory);
        vm.HasDirectoryPicker.Should().BeTrue();
    }

    [Fact]
    public async Task Browse_WhenOptionReadsFileOrDirectory_PicksAFile()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--input", [], "Audit logs", OptionValueKind.TextList, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Inputs, PathKind.FileOrDirectoryToRead);
        var picker = new RecordingFilePickerService("/srv/audits/nas.csv");
        var vm = new TextListOptionViewModel(descriptor, presentation, picker, () => { });

        // Act
        await vm.BrowseCommand.ExecuteAsync(null);

        // Assert
        vm.Values.Should().Equal("/srv/audits/nas.csv");
        picker.Kinds.Should().Equal(PathKind.FileToRead);
    }

    [Fact]
    public void HasDirectoryPicker_WhenOptionIsAnOutputFile_IsFalse()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--html", [], "HTML output", OptionValueKind.Text, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Outputs, PathKind.FileToWrite);

        // Act
        var vm = new TextOptionViewModel(descriptor, presentation, new StubFilePickerService(null), () => { });

        // Assert
        vm.HasDirectoryPicker.Should().BeFalse();
    }

    [Fact]
    public void ApplyTo_WhenAValueIsTypedButNotAdded_StoresItWithTheListedValues()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--include-hosts", [], "Hosts", OptionValueKind.TextList, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Filters, PathKind.None);
        var changes = 0;
        var vm = new TextListOptionViewModel(descriptor, presentation, new StubFilePickerService(null), () => changes++);
        vm.Values.Add("nas*");
        var state = new PortAtlas.Gui.Core.Forms.CommandFormState();

        // Act
        vm.PendingValue = "*.home.arpa";
        vm.ApplyTo(state);

        // Assert
        state.Lists["--include-hosts"].Should().Equal("nas*", "*.home.arpa");
        changes.Should().BeGreaterThan(0);
    }

    [Fact]
    public void LoadFrom_WhenAListIsRestored_ClearsTheTypedValue()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--include-hosts", [], "Hosts", OptionValueKind.TextList, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Filters, PathKind.None);
        var vm = new TextListOptionViewModel(descriptor, presentation, new StubFilePickerService(null), () => { })
        {
            PendingValue = "left-over"
        };
        var state = new PortAtlas.Gui.Core.Forms.CommandFormState();
        state.SetList("--include-hosts", ["nas*"]);

        // Act
        vm.LoadFrom(state);

        // Assert
        vm.Values.Should().Equal("nas*");
        vm.PendingValue.Should().BeEmpty();
    }

    [Fact]
    public void Value_WhenIntegerIsBelowAnyMinimum_IsNotRejectedByTheGui()
    {
        // Arrange
        var descriptor = new OptionDescriptor("--min-observations", [], "Minimum observations", OptionValueKind.Integer, [], false, false, false);
        var presentation = new OptionPresentationEntry(OptionGroup.Filters, PathKind.None);
        var vm = new IntegerOptionViewModel(descriptor, presentation, () => { });

        // Act
        vm.Value = "-5";

        // Assert
        vm.Value.Should().Be("-5");
    }

    private sealed class RecordingFilePickerService(string? result) : IFilePickerService
    {
        public List<PathKind> Kinds { get; } = [];

        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default)
        {
            Kinds.Add(kind);
            return Task.FromResult(result);
        }
    }

    private sealed class StubFilePickerService(string? result) : IFilePickerService
    {
        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
