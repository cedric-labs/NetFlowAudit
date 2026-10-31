using AwesomeAssertions;
using PortAtlas.Cli.CommandLine;
using System.CommandLine;
using System.CommandLine.Completions;
using Xunit;

namespace PortAtlas.Tests.CommandLine;

public sealed class ChoiceOptionCompletionTests
{
    [Fact]
    public void CreateChoice_WhenOptionIsBuilt_ExposesAcceptedValuesAsCompletions()
    {
        // Arrange
        var option = OptionFactory.CreateChoice("--zone", "Zone to keep.", "all", "internal", "public");

        // Act
        var labels = option.GetCompletions(CompletionContext.Empty).Select(item => item.Label).ToArray();

        // Assert
        labels.Should().Equal("all", "internal", "public");
    }

    [Theory]
    [InlineData("map", "--layout", "layered")]
    [InlineData("map", "--drawio-subnets", "off")]
    [InlineData("export", "--zone", "public")]
    public void CommandTree_WhenChoiceOptionIsRead_ExposesItsValues(string command, string optionName, string expectedValue)
    {
        // Arrange
        var parent = PortAtlasCommandLine.CreateRootCommand().Subcommands.Single(c => c.Name == command);
        var owner = parent.Subcommands.Count > 0 ? parent.Subcommands.Single(c => c.Name == "rules") : parent;
        var option = owner.Options.Single(o => o.Name == optionName);

        // Act
        var labels = option.GetCompletions(CompletionContext.Empty).Select(item => item.Label).ToArray();

        // Assert
        labels.Should().Contain(expectedValue);
    }
}
