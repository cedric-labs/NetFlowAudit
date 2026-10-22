using FluentAssertions;
using NetArchTest.Rules;
using NetFlowAudit.Core.Models;
using Xunit;

namespace NetFlowAudit.Tests.Architecture;

public sealed class ArchitectureTests
{
    [Fact]
    public void Core_ShouldNotDependOn_NetworkOrCli()
    {
        // Arrange
        var coreAssembly = typeof(NetworkConnectionRecord).Assembly;

        // Act
        var result = Types.InAssembly(coreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("NetFlowAudit.Network", "NetFlowAudit.Cli")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
}
