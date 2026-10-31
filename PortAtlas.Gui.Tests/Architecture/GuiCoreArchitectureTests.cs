using AwesomeAssertions;
using PortAtlas.Gui.Core.Catalog;

namespace PortAtlas.Gui.Tests.Architecture;

public sealed class GuiCoreArchitectureTests
{
    private static readonly string[] ForbiddenNameParts = ["Helper", "Utils", "Manager", "Common"];

    private static readonly System.Reflection.Assembly CoreAssembly = typeof(CommandCatalog).Assembly;

    // Coverlet injects a Tracker type into the instrumented assembly during the coverage run; it is not project code.
    private static IEnumerable<Type> ProjectTypes() =>
        CoreAssembly.GetTypes().Where(type => type.Namespace?.StartsWith("Coverlet.", StringComparison.Ordinal) != true);

    [Fact]
    public void GuiCore_WhenAssemblyIsInspected_ReferencesNoAvaloniaAssembly()
    {
        // Act
        var references = CoreAssembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);

        // Assert
        references.Should().NotContain(name => name.StartsWith("Avalonia", StringComparison.Ordinal));
    }

    [Fact]
    public void GuiCore_WhenTypesAreNamed_UsesNoCatchAllName()
    {
        // Act
        var offenders = ProjectTypes()
            .Select(type => type.Name)
            .Where(name => ForbiddenNameParts.Any(part => name.Contains(part, StringComparison.Ordinal)));

        // Assert
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void GuiCore_WhenNamespacesAreNamed_UsesNoCatchAllName()
    {
        // Act
        var offenders = ProjectTypes()
            .Select(type => type.Namespace ?? string.Empty)
            .Where(ns => ForbiddenNameParts.Any(part => ns.Contains(part, StringComparison.Ordinal)))
            .Distinct(StringComparer.Ordinal);

        // Assert
        offenders.Should().BeEmpty();
    }
}
