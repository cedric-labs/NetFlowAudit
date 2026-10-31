using System.Net;
using AwesomeAssertions;
using PortAtlas.Core.Resolution;
using Xunit;

namespace PortAtlas.Tests.Resolution;

public sealed class CachedHostNameResolverTests
{
    private sealed class FakeOrganizationResolver(
        Func<IPAddress, string?> resolverFunc,
        Func<IPAddress, IpOwnerMetadata?>? detailsFunc = null) : IIpOrganizationResolver
    {
        public Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(resolverFunc(address));
        }

        public Task<IpOwnerMetadata?> ResolveOwnerDetailsAsync(IPAddress address, CancellationToken cancellationToken = default)
        {
            if (detailsFunc is not null)
            {
                return Task.FromResult(detailsFunc(address));
            }

            var org = resolverFunc(address);
            var metadata = org is not null ? new IpOwnerMetadata(org, "US", "192.0.2.0 - 192.0.2.255", "TEST-NET") : null;
            return Task.FromResult(metadata);
        }

        public void Dispose()
        {
            // No resources to release for test mock
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
        // Arrange: Use a short timeout (10 ms) and a reserved documentation address (TEST-NET)
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
        // Arrange: TEST-NET address without reverse DNS PTR
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

        var hasCached = resolver.TryGetCachedEntry(testIp, out var cachedEntry);
        hasCached.Should().BeTrue();
        cachedEntry.Should().NotBeNull();
        cachedEntry!.HostName.Should().Be("Microsoft Corporation");
        cachedEntry.Organization.Should().Be("Microsoft Corporation");
        cachedEntry.Country.Should().Be("US");
        cachedEntry.Source.Should().Be("RDAP");
    }

    [Fact]
    public async Task ResolveHostNameAsync_WhenDnsFailsAndOrgResolverThrows_ReturnsEmptyStringWithoutThrowing()
    {
        // Arrange: Simulator for offline / network error environment
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
        // Arrange: 52.123.242.181 lacks reverse DNS PTR but belongs to Microsoft Corporation (RDAP answer served locally)
        const string rdapJson = """
            {
              "name": "MSFT",
              "entities": [
                {
                  "roles": [ "registrant" ],
                  "vcardArray": [ "vcard", [ [ "fn", {}, "text", "Microsoft Corporation" ] ] ]
                }
              ]
            }
            """;
        using var httpClient = new HttpClient(new StubRdapHandler(rdapJson));
        using var orgResolver = new RdapIpOrganizationResolver(httpClient, requestTimeout: TimeSpan.FromSeconds(10));
        await using var resolver = new CachedHostNameResolver(
            resolutionTimeout: TimeSpan.FromSeconds(2),
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

    [Fact]
    public async Task ResolveHostNameAsync_WhenCustomHostAliasProvided_ReturnsCustomAliasImmediately()
    {
        // Arrange
        var ip = IPAddress.Parse("203.0.113.30");
        var customAliases = new Dictionary<string, string>
        {
            ["203.0.113.30"] = "nextcloud.home.arpa"
        };

        await using var resolver = new CachedHostNameResolver(
            resolutionTimeout: TimeSpan.FromSeconds(2),
            exclusionEvaluator: null,
            cacheStore: null,
            organizationResolver: null,
            customHostAliases: customAliases);

        // Act
        var result = await resolver.ResolveHostNameAsync(ip);

        // Assert
        result.Should().Be("nextcloud.home.arpa");

        var cached = resolver.TryGetCachedHostName(ip, out var cachedName);
        cached.Should().BeTrue();
        cachedName.Should().Be("nextcloud.home.arpa");

        resolver.TryGetCachedEntry(ip, out var entry);
        entry.Should().NotBeNull();
        entry!.Source.Should().Be("Custom");
    }
    [Fact]
    public async Task ResolveHostNameAsync_WhenPublicIpResolvedViaDns_EnrichesOrganizationAndCountryFromRdap()
    {
        // Arrange
        var ip = IPAddress.Parse("8.8.8.8");
        var details = new IpOwnerMetadata("Google LLC", "US", "8.8.8.0/24", "GOGL");
        var fakeOrg = new FakeOrganizationResolver(_ => "Google LLC", _ => details);

        await using var resolver = new CachedHostNameResolver(
            resolutionTimeout: TimeSpan.FromSeconds(2),
            exclusionEvaluator: null,
            cacheStore: null,
            organizationResolver: fakeOrg);

        // Act
        var result = await resolver.ResolveHostNameAsync(ip);

        // Assert
        result.Should().NotBeNullOrWhiteSpace();

        resolver.TryGetCachedEntry(ip, out var entry).Should().BeTrue();
        entry.Should().NotBeNull();
        entry!.Organization.Should().Be("Google LLC");
        entry.Country.Should().Be("US");
        entry.NetworkRange.Should().Be("8.8.8.0/24");
    }

    private sealed class StubRdapHandler(string json) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
