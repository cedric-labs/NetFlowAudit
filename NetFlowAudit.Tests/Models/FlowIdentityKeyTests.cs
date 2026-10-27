using System.Net;
using AwesomeAssertions;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Models;

public sealed class FlowIdentityKeyTests
{
    [Fact]
    public void Constructor_WhenValueIsValid_SetsNormalizedValue()
    {
        // Arrange
        var rawKey = "  TCP:10.0.0.1:443:nginx  ";

        // Act
        var key = new FlowIdentityKey(rawKey);

        // Assert
        key.Value.Should().Be("TCP:10.0.0.1:443:nginx");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WhenValueIsNullOrWhiteSpace_ThrowsArgumentException(string? invalidValue)
    {
        // Act
        var act = () => new FlowIdentityKey(invalidValue!);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void FromEntry_WhenEntryIsValid_CreatesKeyMatchingEntryAuditKey()
    {
        // Arrange
        var host = new ServerHostMetadata("SRV", "Linux", "10.0.0.1");
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("10.0.0.1"), 51234),
            new SocketEndpoint(IPAddress.Parse("1.1.1.1"), 443),
            "Established",
            new ProcessMetadata(1234, "curl", "/usr/bin/curl", "", "", "root"));

        var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, host, socket);

        // Act
        var key = FlowIdentityKey.FromEntry(entry);

        // Assert
        key.Value.Should().Be(entry.ComputeAuditKey());
        key.ToString().Should().Be(entry.ComputeAuditKey());
    }

    [Fact]
    public void FromEntry_WhenEntryIsNull_ThrowsArgumentNullException()
    {
        // Act
        var act = () => FlowIdentityKey.FromEntry(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Equals_WhenValuesAreEqual_ReturnsTrue()
    {
        // Arrange
        var key1 = new FlowIdentityKey("TCP:10.0.0.1:80");
        var key2 = new FlowIdentityKey("TCP:10.0.0.1:80");

        // Act & Assert
        (key1 == key2).Should().BeTrue();
        key1.Equals(key2).Should().BeTrue();
    }
}
