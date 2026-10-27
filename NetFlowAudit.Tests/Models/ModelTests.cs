using System.Net;
using AwesomeAssertions;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
using Xunit;

namespace NetFlowAudit.Tests.Models;

public sealed class ModelTests
{
    [Fact]
    public void NetworkAuditEntry_ComputeAuditKey_DelegatesToSocketKey()
    {
        // Arrange
        var host = new ServerHostMetadata("SRV", "OS", "127.0.0.1");
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Loopback, 50000),
            new SocketEndpoint(IPAddress.Loopback, 8080),
            "Established",
            new ProcessMetadata(10, "proc", "path", "", "", "usr"));

        var entry = new NetworkAuditEntry(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, host, socket);

        // Act & Assert
        entry.ComputeAuditKey().Should().Be(socket.ComputeLogicalKey());
    }
}
