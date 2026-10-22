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
}
