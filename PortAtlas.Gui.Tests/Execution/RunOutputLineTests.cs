using AwesomeAssertions;
using PortAtlas.Gui.Core.Execution;

namespace PortAtlas.Gui.Tests.Execution;

public sealed class RunOutputLineTests
{
    [Theory]
    [InlineData(RunOutputStream.Error, true)]
    [InlineData(RunOutputStream.Standard, false)]
    public void IsError_WhenStreamIsGiven_ReflectsTheErrorStream(RunOutputStream stream, bool expected)
    {
        // Arrange
        var line = new RunOutputLine(stream, "text");

        // Act
        var isError = line.IsError;

        // Assert
        isError.Should().Be(expected);
    }
}
