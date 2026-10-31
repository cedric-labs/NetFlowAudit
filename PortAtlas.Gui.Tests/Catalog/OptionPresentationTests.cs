using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;

namespace PortAtlas.Gui.Tests.Catalog;

public sealed class OptionPresentationTests
{
    [Fact]
    public void Coverage_WhenCatalogIsBuilt_HasAnEntryForEveryOption()
    {
        // Arrange
        var optionNames = CommandCatalog.Build().Commands
            .SelectMany(c => c.Options)
            .Select(o => o.Name)
            .ToHashSet(StringComparer.Ordinal);

        // Act
        var withoutEntry = optionNames.Except(OptionPresentation.Entries.Keys);
        var withoutOption = OptionPresentation.Entries.Keys.Except(optionNames);

        // Assert
        withoutEntry.Should().BeEmpty("every option of the catalog needs a presentation entry");
        withoutOption.Should().BeEmpty("every presentation entry must name an option of the catalog");
    }
}
