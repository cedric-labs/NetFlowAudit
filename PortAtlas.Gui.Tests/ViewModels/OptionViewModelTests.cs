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

    private sealed class StubFilePickerService(string? result) : IFilePickerService
    {
        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult(result);
    }
}
