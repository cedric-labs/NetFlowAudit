using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;

namespace PortAtlas.Gui.Tests.Catalog;

public sealed class CommandCatalogTests
{
    [Fact]
    public void Build_WhenRootCommandIsRead_ListsEveryLeafCommandExceptAudit()
    {
        // Act
        var catalog = CommandCatalog.Build();

        // Assert
        catalog.Commands.Select(c => c.Key).Should().BeEquivalentTo(
            "map", "export rules", "export ndjson", "resolve", "lookup", "config init");
    }

    [Fact]
    public void Build_WhenOptionIsChoice_ExposesAcceptedValues()
    {
        // Arrange
        var catalog = CommandCatalog.Build();

        // Act
        var zone = catalog.Find("export rules")?.Options.Single(o => o.Name == "--zone");

        // Assert
        zone.Should().NotBeNull();
        zone.Kind.Should().Be(OptionValueKind.Choice);
        zone.AcceptedValues.Should().Equal("all", "internal", "public");
    }

    [Fact]
    public void Build_WhenOptionIsRepeatableList_FlagsItAsRepeatable()
    {
        // Arrange
        var map = CommandCatalog.Build().Find("map");

        // Act
        var input = map?.Options.Single(o => o.Name == "--input");
        var includePorts = map?.Options.Single(o => o.Name == "--include-ports");

        // Assert
        input.Should().NotBeNull();
        input.IsRepeatable.Should().BeTrue();
        includePorts.Should().NotBeNull();
        includePorts.Kind.Should().Be(OptionValueKind.TextList);
        includePorts.IsRepeatable.Should().BeTrue();
    }

    [Fact]
    public void Build_WhenCommandUsesGlobalOption_IncludesConfigOnEveryCommand()
    {
        // Act
        var catalog = CommandCatalog.Build();

        // Assert
        catalog.Commands.Should().OnlyContain(c => c.Options.Any(o => o.Name == "--config" && o.IsGlobal));
    }

    [Fact]
    public void Build_WhenCommandsAreDescribed_OmitsHelpOptionAndKeepsPositionalArguments()
    {
        // Arrange
        var catalog = CommandCatalog.Build();

        // Act
        var lookup = catalog.Find("lookup");

        // Assert
        catalog.Commands.SelectMany(c => c.Options).Should().NotContain(o => o.Name == "--help" || o.Name == "--version");
        lookup.Should().NotBeNull();
        lookup.Arguments.Should().ContainSingle(a => a.Name == "ip" && a.IsRequired);
    }
}
