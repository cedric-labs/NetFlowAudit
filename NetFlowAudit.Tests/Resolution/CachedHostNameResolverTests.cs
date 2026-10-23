using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

public sealed class CachedHostNameResolverTests
{
    [Fact]
    public Task ResolveHostNameAsync_WhenNullAddress_ThrowsArgumentNullException()
    {
        // Arrange
        var resolver = new CachedHostNameResolver();

        // Act & Assert
        return Assert.ThrowsAsync<ArgumentNullException>(() => resolver.ResolveHostNameAsync(null!));
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenAnyAddress_ReturnsEmptyString()
    {
        // Arrange
        var resolver = new CachedHostNameResolver();

        // Act
        var resultV4 = await resolver.ResolveHostNameAsync(IPAddress.Any);
        var resultV6 = await resolver.ResolveHostNameAsync(IPAddress.IPv6Any);

        // Assert
        resultV4.Should().BeEmpty();
        resultV6.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenLoopbackAddress_ReturnsLocalhost()
    {
        // Arrange
        var resolver = new CachedHostNameResolver();

        // Act
        var resultV4 = await resolver.ResolveHostNameAsync(IPAddress.Loopback);
        var resultV6 = await resolver.ResolveHostNameAsync(IPAddress.IPv6Loopback);

        // Assert
        resultV4.Should().Be("localhost");
        resultV6.Should().Be("localhost");
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenNonRoutableOrTimeoutAddress_ReturnsEmptyAndCachesNegativeResult()
    {
        // Arrange : Utilise un délai très court (10 ms) et une adresse réservée pour documentation (TEST-NET)
        var resolver = new CachedHostNameResolver(TimeSpan.FromMilliseconds(10));
        var dummyIp = IPAddress.Parse("192.0.2.1");

        // Act
        var firstResult = await resolver.ResolveHostNameAsync(dummyIp);
        var secondResult = await resolver.ResolveHostNameAsync(dummyIp);

        // Assert
        firstResult.Should().BeEmpty();
        secondResult.Should().BeEmpty();
    }
}
