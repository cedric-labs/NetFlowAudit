using AwesomeAssertions;
using NetArchTest.Rules;
using PortAtlas.Core.Models;
using Xunit;

namespace PortAtlas.Tests.Architecture;

public sealed class ArchitectureTests
{
    [Fact]
    public void Dependencies_WhenCheckingCoreAssembly_DoesNotDependOnCli()
    {
        // Arrange
        var coreAssembly = typeof(NetworkAuditEntry).Assembly;

        // Act
        var result = Types.InAssembly(coreAssembly)
            .ShouldNot()
            .HaveDependencyOnAny("PortAtlas.Cli")
            .GetResult();

        // Assert
        result.IsSuccessful.Should().BeTrue();
    }

    [Fact]
    public void NamingConvention_WhenInspectingCoreTypes_DoesNotEndWithForbiddenGenericSuffixes()
    {
        // Arrange
        var forbiddenSuffixes = new[] { "Helper", "Helpers", "Utils", "Util", "Manager", "Common" };
        var types = typeof(NetworkAuditEntry).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.Name.StartsWith("<", StringComparison.Ordinal))
            .ToList();

        // Act & Assert
        types.Should().NotBeEmpty();
        foreach (var type in types)
        {
            foreach (var suffix in forbiddenSuffixes)
            {
                type.Name.Should().NotEndWith(suffix, $"Class '{type.FullName}' violates Rule 3 (No generic suffixes).");
            }
        }
    }

    [Fact]
    public void NamingConvention_WhenInspectingAllTypes_DoesNotContainAiBuzzwords()
    {
        // Arrange
        var aiBuzzwords = new[] { "Robust", "Seamless", "Smart", "Resilient", "Advanced", "Comprehensive", "Ultimate" };
        var types = typeof(NetworkAuditEntry).Assembly.GetTypes()
            .Where(t => !t.Name.StartsWith("<", StringComparison.Ordinal))
            .ToList();

        // Act & Assert
        types.Should().NotBeEmpty();
        foreach (var type in types)
        {
            foreach (var buzzword in aiBuzzwords)
            {
                type.Name.Should().NotContain(buzzword, $"Type '{type.FullName}' violates Rule 6 (No AI buzzwords).");
            }
        }
    }

    [Fact]
    public void NamingConvention_WhenInspectingTestMethods_FollowsStrictThreePartNamingPattern()
    {
        // Arrange
        var testAssembly = typeof(ArchitectureTests).Assembly;
        var testMethods = testAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract)
            .SelectMany(t => t.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            .Where(m => m.GetCustomAttributes(typeof(FactAttribute), inherit: true).Length > 0 ||
                        m.GetCustomAttributes(typeof(TheoryAttribute), inherit: true).Length > 0)
            .ToList();

        // Act & Assert
        testMethods.Should().NotBeEmpty();
        foreach (var method in testMethods)
        {
            var parts = method.Name.Split('_');
            parts.Length.Should().Be(3, $"Test method '{method.DeclaringType?.Name}.{method.Name}' must strictly follow 'Method_Condition_ExpectedResult' pattern (Rule 7).");
        }
    }
}
