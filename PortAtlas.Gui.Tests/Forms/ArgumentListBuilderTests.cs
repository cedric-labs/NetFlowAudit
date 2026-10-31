using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;
using PortAtlas.Gui.Core.Forms;

namespace PortAtlas.Gui.Tests.Forms;

public sealed class ArgumentListBuilderTests
{
    private static CommandDescriptor Command(string key) =>
        CommandCatalog.Build().Find(key) ?? throw new InvalidOperationException($"The catalog has no command '{key}'.");

    [Fact]
    public void Build_WhenNoOptionIsSet_ReturnsOnlyTheCommandPath()
    {
        // Act
        var line = ArgumentListBuilder.Build(Command("export rules"), new CommandFormState());

        // Assert
        line.Arguments.Should().Equal("export", "rules");
        line.Preview.Should().Be("export rules");
    }

    [Fact]
    public void Build_WhenRepeatableOptionHasTwoValues_RepeatsTheOption()
    {
        // Arrange
        var state = new CommandFormState();
        state.SetList("--input", ["a.csv", "b.csv"]);

        // Act
        var line = ArgumentListBuilder.Build(Command("map"), state);

        // Assert
        line.Arguments.Should().Equal("map", "--input", "a.csv", "--input", "b.csv");
    }

    [Fact]
    public void Build_WhenFlagIsTrue_EmitsFlagWithoutValue()
    {
        // Arrange
        var state = new CommandFormState();
        state.SetFlag("--cache-only", true);

        // Act
        var line = ArgumentListBuilder.Build(Command("resolve"), state);

        // Assert
        line.Arguments.Should().Equal("resolve", "--cache-only");
    }

    [Fact]
    public void Build_WhenFlagIsFalse_EmitsNothing()
    {
        // Arrange
        var state = new CommandFormState();
        state.SetFlag("--cache-only", false);

        // Act
        var line = ArgumentListBuilder.Build(Command("resolve"), state);

        // Assert
        line.Arguments.Should().Equal("resolve");
    }

    [Fact]
    public void Build_WhenValueIsBlank_EmitsNothingSoTheCliAppliesItsDefault()
    {
        // Arrange
        var state = new CommandFormState();
        state.SetValue("--layout", "  ");

        // Act
        var line = ArgumentListBuilder.Build(Command("map"), state);

        // Assert
        line.Arguments.Should().Equal("map");
    }

    [Fact]
    public void Build_WhenValueContainsSpaces_KeepsItAsOneArgumentAndQuotesThePreview()
    {
        // Arrange
        var state = new CommandFormState();
        state.SetValue("--html", @"C:\Network maps\homelab.html");

        // Act
        var line = ArgumentListBuilder.Build(Command("map"), state);

        // Assert
        line.Arguments.Should().Equal("map", "--html", @"C:\Network maps\homelab.html");
        line.Preview.Should().Be("map --html \"C:\\Network maps\\homelab.html\"");
    }

    [Fact]
    public void Build_WhenPositionalArgumentIsSet_AppendsItLast()
    {
        // Arrange
        var state = new CommandFormState();
        state.SetValue("--dns-timeout-ms", "500");
        state.SetArgument("ip", "192.168.1.10");

        // Act
        var line = ArgumentListBuilder.Build(Command("lookup"), state);

        // Assert
        line.Arguments.Should().Equal("lookup", "--dns-timeout-ms", "500", "192.168.1.10");
    }
}
