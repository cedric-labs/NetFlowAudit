using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using AwesomeAssertions;
using Xunit;

namespace PortAtlas.Tests.Architecture;

public sealed class ShippedAssemblyVersionTests
{
    /// <summary>
    /// Le runtime refuse de charger une assembly dont la version est inférieure à celle qu'une autre assembly référence :
    /// l'exécutable publié plante alors au premier appel, alors que la compilation et les tests restent verts.
    /// Ce test lit les références dans les métadonnées, sans charger les assemblies, pour couvrir tout ce qui est livré.
    /// </summary>
    [Fact]
    public void ShippedAssemblies_WhenReferencingAnotherShippedAssembly_FindAVersionAtLeastAsHigh()
    {
        // Arrange
        var shippedVersions = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Where(path => IsShippedByPortAtlas(Path.GetFileNameWithoutExtension(path)))
            .ToDictionary(path => Path.GetFileNameWithoutExtension(path), path => AssemblyName.GetAssemblyName(path).Version, StringComparer.OrdinalIgnoreCase);

        // Act
        var mismatches = shippedVersions.Keys
            .SelectMany(name => ReadReferences(Path.Combine(AppContext.BaseDirectory, name + ".dll"))
                .Where(reference => shippedVersions.TryGetValue(reference.Name, out var available) && available < reference.Version)
                .Select(reference => $"{name} référence {reference.Name} {reference.Version}, livré en {shippedVersions[reference.Name]}"))
            .ToList();

        // Assert
        shippedVersions.Should().ContainKeys("PortAtlas", "PortAtlas.Core", "ProcessForge", "SocketForge", "TopologyForge");
        mismatches.Should().BeEmpty();
    }

    private static bool IsShippedByPortAtlas(string assemblyName) =>
        (assemblyName.StartsWith("PortAtlas.", StringComparison.Ordinal) || string.Equals(assemblyName, "PortAtlas", StringComparison.Ordinal)) && !assemblyName.EndsWith(".Tests", StringComparison.Ordinal)
        || assemblyName.EndsWith("Forge", StringComparison.Ordinal)
        || assemblyName.StartsWith("FileForge.", StringComparison.Ordinal);

    private static IReadOnlyList<(string Name, Version Version)> ReadReferences(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();
        return metadata.AssemblyReferences
            .Select(handle => metadata.GetAssemblyReference(handle))
            .Select(reference => (metadata.GetString(reference.Name), reference.Version))
            .ToList();
    }
}
