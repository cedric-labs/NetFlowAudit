using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using AwesomeAssertions;
using Xunit;

namespace PortAtlas.Tests.Architecture;

public sealed class ShippedAssemblyVersionTests
{
    /// <summary>
    /// The runtime refuses to load an assembly whose version is lower than the one another assembly references:
    /// the published executable then crashes on the first call, while the build and the tests stay green.
    /// This test reads the references from the metadata, without loading the assemblies, to cover everything shipped.
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
                .Select(reference => $"{name} references {reference.Name} {reference.Version}, shipped as {shippedVersions[reference.Name]}"))
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
