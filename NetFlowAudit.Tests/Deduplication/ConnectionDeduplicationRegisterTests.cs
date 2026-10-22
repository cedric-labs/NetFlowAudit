using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Models;
using Xunit;

namespace NetFlowAudit.Tests.Deduplication;

public sealed class ConnectionDeduplicationRegisterTests
{
    private static readonly ServerHostMetadata DummyServer = new("SRV-PROD-01", "Windows Server 2022", "192.168.1.10");

    [Fact]
    public void TryRegisterOrUpdate_WhenConnectionIsNew_ReturnsTrue()
    {
        // Arrange
        var register = new ConnectionDeduplicationRegister();
        var record = new NetworkConnectionRecord(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            NetworkTransportProtocol.Tcp,
            FlowDirection.Outbound,
            new NetworkEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
            new NetworkEndpoint(IPAddress.Parse("10.0.0.5"), 443),
            "Established",
            new ProcessMetadata(1234, "curl", @"C:\Windows\System32\curl.exe", "", "", "User1"));

        // Act
        var isNew = register.TryRegisterOrUpdate(record, out var registered);

        // Assert
        isNew.Should().BeTrue();
        registered.ObservationCount.Should().Be(1);
        register.UniqueCount.Should().Be(1);
    }

    [Fact]
    public void TryRegisterOrUpdate_WhenOutboundFlowRepeatsWithDifferentEphemeralPort_ReturnsFalseAndIncrementsCount()
    {
        // Arrange
        var register = new ConnectionDeduplicationRegister();
        var record1 = new NetworkConnectionRecord(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            NetworkTransportProtocol.Tcp,
            FlowDirection.Outbound,
            new NetworkEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
            new NetworkEndpoint(IPAddress.Parse("10.0.0.5"), 443),
            "Established",
            new ProcessMetadata(1234, "curl", @"C:\Windows\System32\curl.exe", "", "", "User1"));

        var record2 = new NetworkConnectionRecord(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            NetworkTransportProtocol.Tcp,
            FlowDirection.Outbound,
            new NetworkEndpoint(IPAddress.Parse("192.168.1.10"), 50124), // Nouveau port éphémère local
            new NetworkEndpoint(IPAddress.Parse("10.0.0.5"), 443),
            "Established",
            new ProcessMetadata(1234, "curl", @"C:\Windows\System32\curl.exe", "", "", "User1"));

        // Act
        var firstResult = register.TryRegisterOrUpdate(record1, out _);
        var secondResult = register.TryRegisterOrUpdate(record2, out var secondRegistered);

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
        secondRegistered.ObservationCount.Should().Be(2);
        register.UniqueCount.Should().Be(1);
    }
}
