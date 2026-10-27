using System.Net;
using AwesomeAssertions;
using NetFlowAudit.Core.Deduplication;
using NetFlowAudit.Core.Models;
using ProcessForge.Models;
using SocketForge.Models;
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
        var socket = new ActiveSocketConnection(
            SocketTransportProtocol.Tcp,
            SocketFlowDirection.Outbound,
            new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
            new SocketEndpoint(IPAddress.Parse("10.0.0.5"), 443),
            "Established",
            new ProcessMetadata(1234, "curl", @"C:\Windows\System32\curl.exe", "", "", "User1"));

        var record = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            socket);

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
        var proc = new ProcessMetadata(1234, "curl", @"C:\Windows\System32\curl.exe", "", "", "User1");

        var record1 = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
                new SocketEndpoint(IPAddress.Parse("10.0.0.5"), 443),
                "Established",
                proc));

        var record2 = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50124),
                new SocketEndpoint(IPAddress.Parse("10.0.0.5"), 443),
                "Established",
                proc));

        // Act
        var firstResult = register.TryRegisterOrUpdate(record1, out _);
        var secondResult = register.TryRegisterOrUpdate(record2, out var secondRegistered);

        // Assert
        firstResult.Should().BeTrue();
        secondResult.Should().BeFalse();
        secondRegistered.ObservationCount.Should().Be(2);
        register.UniqueCount.Should().Be(1);
    }

    [Fact]
    public void GetAllEntries_WhenEntriesRegistered_ReturnsNonNullEntriesSnapshot()
    {
        // Arrange
        var register = new ConnectionDeduplicationRegister();
        register.Hydrate(["hydrated-key-without-entry"]);

        var proc = new ProcessMetadata(1234, "curl", @"C:\Windows\System32\curl.exe", "", "", "User1");
        var entry = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
                new SocketEndpoint(IPAddress.Parse("10.0.0.5"), 443),
                "Established",
                proc));

        register.TryRegisterOrUpdate(entry, out _);

        // Act
        var allEntries = register.GetAllEntries();

        // Assert
        allEntries.Should().ContainSingle();
        allEntries[0].Server.HostName.Should().Be("SRV-PROD-01");
    }

    [Fact]
    public void Hydrate_WhenNetworkAuditEntriesProvided_PopulatesRegisterAndNeutralizesSubsequentDuplicates()
    {
        // Arrange
        var register = new ConnectionDeduplicationRegister();
        var proc = new ProcessMetadata(1234, "curl", @"C:\Windows\System32\curl.exe", "", "", "User1");
        var existingEntry = new NetworkAuditEntry(
            DateTimeOffset.UtcNow.AddMinutes(-10),
            DateTimeOffset.UtcNow.AddMinutes(-5),
            7,
            DummyServer,
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 50123),
                new SocketEndpoint(IPAddress.Parse("10.0.0.5"), 443),
                "Established",
                proc),
            RemoteHostName: "api.internal");

        // Act - Hydrate with historical entries
        register.Hydrate([existingEntry]);

        // Assert - Register count is updated and entry is returned in GetAllEntries
        register.UniqueCount.Should().Be(1);
        var entries = register.GetAllEntries();
        entries.Should().ContainSingle();
        entries[0].ObservationCount.Should().Be(7);
        entries[0].RemoteHostName.Should().Be("api.internal");

        // Act 2 - Live flow seen with different ephemeral port
        var liveCandidate = new NetworkAuditEntry(
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            1,
            DummyServer,
            new ActiveSocketConnection(
                SocketTransportProtocol.Tcp,
                SocketFlowDirection.Outbound,
                new SocketEndpoint(IPAddress.Parse("192.168.1.10"), 59999),
                new SocketEndpoint(IPAddress.Parse("10.0.0.5"), 443),
                "Established",
                proc));

        var isNew = register.TryRegisterOrUpdate(liveCandidate, out var registered);

        // Assert 2 - Not considered new, count incremented from 7 to 8
        isNew.Should().BeFalse();
        registered.ObservationCount.Should().Be(8);
        registered.RemoteHostName.Should().Be("api.internal");
    }
}
