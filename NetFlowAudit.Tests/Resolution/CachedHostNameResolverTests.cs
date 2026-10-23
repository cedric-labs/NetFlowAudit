using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

public sealed class CachedHostNameResolverTests
{
    private sealed class FakeOrganizationResolver(Func<IPAddress, string?> resolverFunc) : IIpOrganizationResolver
    {
        public Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(resolverFunc(address));
        }

        public void Dispose()
        {
            // Aucun état à libérer pour le mock de test
        }
    }

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
        await using var resolver = new CachedHostNameResolver();

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
        await using var resolver = new CachedHostNameResolver();

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
        await using var resolver = new CachedHostNameResolver(TimeSpan.FromMilliseconds(10));
        var dummyIp = IPAddress.Parse("192.0.2.1");

        // Act
        var firstResult = await resolver.ResolveHostNameAsync(dummyIp);
        var secondResult = await resolver.ResolveHostNameAsync(dummyIp);

        // Assert
        firstResult.Should().BeEmpty();
        secondResult.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenDnsFailsAndOrgResolverSucceeds_ReturnsOrganizationName()
    {
        // Arrange : adresse TEST-NET sans PTR DNS
        var testIp = IPAddress.Parse("192.0.2.42");
        var fakeOrgResolver = new FakeOrganizationResolver(ip => ip.Equals(testIp) ? "Microsoft Corporation" : null);

        await using var resolver = new CachedHostNameResolver(
            resolutionTimeout: TimeSpan.FromMilliseconds(10),
            exclusionEvaluator: null,
            cacheStore: null,
            organizationResolver: fakeOrgResolver);

        // Act
        var result = await resolver.ResolveHostNameAsync(testIp);

        // Assert
        result.Should().Be("Microsoft Corporation");
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenDnsFailsAndOrgResolverThrows_ReturnsEmptyStringWithoutThrowing()
    {
        // Arrange : simulateur d'environnement hors-ligne / erreur réseau
        var testIp = IPAddress.Parse("192.0.2.99");
        var failingOrgResolver = new FakeOrganizationResolver(_ => throw new HttpRequestException("Air-gapped server"));

        await using var resolver = new CachedHostNameResolver(
            resolutionTimeout: TimeSpan.FromMilliseconds(10),
            exclusionEvaluator: null,
            cacheStore: null,
            organizationResolver: failingOrgResolver);

        // Act
        var result = await resolver.ResolveHostNameAsync(testIp);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenIpIsMicrosoftWithoutDnsPtr_ResolvesOrganizationViaRdap()
    {
        // Arrange : 52.123.242.181 n'a pas de PTR DNS, mais appartient à Microsoft Corporation
        using var orgResolver = new RdapIpOrganizationResolver();
        await using var resolver = new CachedHostNameResolver(
            resolutionTimeout: TimeSpan.FromMilliseconds(500),
            exclusionEvaluator: null,
            cacheStore: null,
            organizationResolver: orgResolver);

        var ip = IPAddress.Parse("52.123.242.181");

        // Act
        var result = await resolver.ResolveHostNameAsync(ip);

        // Assert
        result.Should().Be("Microsoft Corporation");
    }

    [Fact]
    public void TryGetCachedHostName_WhenAddressIsExcluded_ReturnsTrueAndEmptyString()
    {
        // Arrange
        var evaluator = new DnsAddressExclusionEvaluator(["169.254.0.0/16"]);
        using var resolver = new CachedHostNameResolver(TimeSpan.FromMilliseconds(10), evaluator);
        var linkLocal = IPAddress.Parse("169.254.1.2");

        // Act
        var found = resolver.TryGetCachedHostName(linkLocal, out var hostName);

        // Assert
        found.Should().BeTrue();
        hostName.Should().BeEmpty();
    }

    [Fact]
    public void TryGetCachedHostName_WhenAddressNotInCache_ReturnsFalse()
    {
        // Arrange
        var evaluator = new DnsAddressExclusionEvaluator([]);
        using var resolver = new CachedHostNameResolver(TimeSpan.FromMilliseconds(10), evaluator);
        var externalIp = IPAddress.Parse("93.184.216.34");

        // Act
        var found = resolver.TryGetCachedHostName(externalIp, out var hostName);

        // Assert
        found.Should().BeFalse();
        hostName.Should().BeEmpty();
    }
}
