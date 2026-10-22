using FluentAssertions;
using NetArchTest.Rules;
using NetFlowAudit.Core.Models;
using Xunit;

namespace NetFlowAudit.Tests.Architecture;

public sealed class ArchitectureTests
{
    [Fact]
    public void Core_ShouldNotDependOn_Cli()
    {
        // Arrange
        var coreAssembly = typeof(NetworkAuditEntry).Assembly;

        // Act
        var result = Types.InAssembly(coreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("NetFlowAudit.Cli")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }
}
