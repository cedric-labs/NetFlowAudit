using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Interaction;
using PortAtlas.Gui.Core.ViewModels;

namespace PortAtlas.Gui.Tests.Catalog;

public sealed class ChoiceValueDescriptionsTests
{
    private static List<OptionDescriptor> ChoiceOptions() =>
        [.. CommandCatalog.Build().Commands.SelectMany(command => command.Options)
            .Where(option => option.AcceptedValues.Count > 0)
            .DistinctBy(option => option.Name)];

    [Fact]
    public void For_WhenEveryCliChoiceValueIsLookedUp_ReturnsADescription()
    {
        // Act
        var missing = ChoiceOptions()
            .SelectMany(option => option.AcceptedValues.Select(value => (option.Name, value)))
            .Where(pair => ChoiceValueDescriptions.For(pair.Name, pair.value).Length == 0)
            .ToList();

        // Assert
        missing.Should().BeEmpty();
    }

    [Fact]
    public void Entries_WhenCompared_DeclareOnlyValuesTheCliAccepts()
    {
        // Arrange
        var accepted = ChoiceOptions().ToDictionary(option => option.Name, option => option.AcceptedValues);

        // Act
        var unknown = ChoiceValueDescriptions.Entries
            .SelectMany(entry => entry.Value.Keys.Select(value => (Option: entry.Key, Value: value)))
            .Where(pair => !accepted.TryGetValue(pair.Option, out var values) || !values.Contains(pair.Value))
            .ToList();

        // Assert
        unknown.Should().BeEmpty();
    }

    [Fact]
    public void SelectedValueDescription_WhenValueIsChosen_ReturnsItsExplanation()
    {
        // Arrange
        var command = CommandCatalog.Build().Find("map") ?? throw new InvalidOperationException("Command 'map' not found.");
        var form = new CommandFormViewModel(command, new NullFilePicker());
        var routing = form.Options.OfType<ChoiceOptionViewModel>().Single(option => option.Name == "--routing");

        // Act
        routing.SelectedValue = "bus";

        // Assert
        routing.SelectedChoice?.Value.Should().Be("bus");
        routing.SelectedValueDescription.Should().Contain("trunk");
    }

    private sealed class NullFilePicker : IFilePickerService
    {
        public Task<string?> PickAsync(PathKind kind, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);
    }
}
