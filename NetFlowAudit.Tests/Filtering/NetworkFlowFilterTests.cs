using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Filtering;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Filtering;

public sealed class NetworkFlowFilterTests
{
    private static readonly ProcessMetadata DefaultProc = new(100, "curl", "/bin/curl", "", "", "user");

    [Fact]
    public void Constructor_WhenOptionsIsNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new NetworkFlowFilter(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShouldExclude_WhenConnectionIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        var filter = new NetworkFlowFilter(new NetworkFlowFilterOptions());

        // Act & Assert
        var act = () => filter.ShouldExclude(null!, new HashSet<IPAddress>());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ShouldExclude_WhenLocalPortIsExcluded_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludedLocalPorts = new HashSet<int> { 22, 9100 }
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 22),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenRemotePortIsExcluded_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludedRemotePorts = new HashSet<int> { 53, 123 }
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
            new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 53),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenLocalAndRemoteIpAreEqual_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions { ExcludeLoopbackFlows = true };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.50"), 54321),
            new SocketEndpoint(IPAddress.Parse("192.168.1.50"), 8080),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenBothAddressesAreLoopback_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions { ExcludeLoopbackFlows = true };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("127.0.0.1"), 50000),
            new SocketEndpoint(IPAddress.Parse("127.0.0.53"), 53),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenBothAddressesBelongToHostInterfaces_ReturnsTrue()
    {
        // Arrange
        var hostIps = new HashSet<IPAddress>
        {
            IPAddress.Parse("10.0.0.5"),
            IPAddress.Parse("192.168.1.100")
        };
        var options = new NetworkFlowFilterOptions { ExcludeLoopbackFlows = true };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("10.0.0.5"), 45000),
            new SocketEndpoint(IPAddress.Parse("192.168.1.100"), 8080),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, hostIps).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenExcludeLoopbackIsFalse_ReturnsFalseForLoopback()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions { ExcludeLoopbackFlows = false };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("127.0.0.1"), 50000),
            new SocketEndpoint(IPAddress.Parse("127.0.0.1"), 8080),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeFalse();
    }

    [Fact]
    public void ShouldExclude_WhenSocketIsListeningOnAnyAddress_DoesNotExcludeAsLoopback()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions { ExcludeLoopbackFlows = true };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 8080),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeFalse();
    }

    [Fact]
    public void ShouldExclude_WhenSocketIsListeningOnLoopbackAddress_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions { ExcludeLoopbackFlows = true };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Loopback, 65385),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenNormalOutboundFlow_ReturnsFalse()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludeLoopbackFlows = true,
            ExcludedLocalPorts = new HashSet<int> { 22 },
            ExcludedRemotePorts = new HashSet<int> { 53 }
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000),
            new SocketEndpoint(IPAddress.Parse("142.250.74.206"), 443),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress> { IPAddress.Parse("192.168.1.10") }).Should().BeFalse();
    }

    [Fact]
    public void ShouldExclude_WhenRemoteIpMatchesConfiguredExactIp_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludedRemoteIps = ["8.8.8.8", "1.1.1.1"]
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000),
            new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 443),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenRemoteIpMatchesConfiguredCidr_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludedRemoteIps = ["10.0.0.0/8"]
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000),
            new SocketEndpoint(IPAddress.Parse("10.50.1.20"), 8080),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenRemoteIpDoesNotMatchExcludedIps_ReturnsFalse()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludedRemoteIps = ["10.0.0.0/8"]
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50000),
            new SocketEndpoint(IPAddress.Parse("142.250.74.206"), 443),
            "Established",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeFalse();
    }

    [Fact]
    public void ShouldExclude_WhenRemoteIpIsAnyAndConfiguredInExcludedRemoteIps_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludedRemoteIps = ["0.0.0.0"]
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 8080),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ShouldExclude_WhenRemotePortIsZeroAndConfiguredInExcludedRemotePorts_ReturnsTrue()
    {
        // Arrange
        var options = new NetworkFlowFilterOptions
        {
            ExcludedRemotePorts = new HashSet<int> { 0 }
        };
        var filter = new NetworkFlowFilter(options);
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 8080),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            DefaultProc);

        // Act & Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ExcludeLocalPort_WhenAddedDynamically_ExcludesSubsequentMatchingFlow()
    {
        // Arrange
        var filter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Listening,
            new SocketEndpoint(IPAddress.Any, 9200),
            new SocketEndpoint(IPAddress.Any, 0),
            "Listen",
            DefaultProc);

        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeFalse();

        // Act
        filter.ExcludeLocalPort(9200);

        // Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ExcludeRemotePort_WhenAddedDynamically_ExcludesSubsequentMatchingFlow()
    {
        // Arrange
        var filter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
            new SocketEndpoint(IPAddress.Parse("8.8.8.8"), 8443),
            "Established",
            DefaultProc);

        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeFalse();

        // Act
        filter.ExcludeRemotePort(8443);

        // Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }

    [Fact]
    public void ExcludeRemoteIp_WhenAddedDynamically_ExcludesSubsequentMatchingFlow()
    {
        // Arrange
        var filter = new NetworkFlowFilter(new NetworkFlowFilterOptions());
        var conn = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
            new SocketEndpoint(IPAddress.Parse("52.123.242.181"), 443),
            "Established",
            DefaultProc);

        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeFalse();

        // Act
        filter.ExcludeRemoteIp("52.123.242.181");

        // Assert
        filter.ShouldExclude(conn, new HashSet<IPAddress>()).Should().BeTrue();
    }
}
