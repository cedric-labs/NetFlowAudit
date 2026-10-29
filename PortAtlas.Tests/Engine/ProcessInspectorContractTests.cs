using AwesomeAssertions;
using ProcessForge.Inspectors;
using Xunit;

namespace PortAtlas.Tests.Engine;

/// <summary>
/// Pins what the audit expects from the ProcessForge inspector it uses: the UserName column of the audit log and the
/// ExcludeSystemAccounts filter both depend on the account being returned.
/// </summary>
public sealed class ProcessInspectorContractTests
{
    [Fact]
    public async Task ResolveProcessAsync_WhenProcessIsCurrentProcess_ReturnsTheAccountRunningIt()
    {
        // Arrange
        var inspector = new CrossPlatformProcessInspector();

        // Act
        var metadata = await inspector.ResolveProcessAsync(Environment.ProcessId);

        // Assert
        metadata.UserName.Should().EndWith(Environment.UserName);
    }
}
