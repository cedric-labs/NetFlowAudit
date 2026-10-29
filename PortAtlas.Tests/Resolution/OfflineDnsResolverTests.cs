using System.Net;
using AwesomeAssertions;
using PortAtlas.Core.Resolution;
using Xunit;

namespace PortAtlas.Tests.Resolution;

public sealed class OfflineDnsResolverTests
{
    [Fact]
    public async Task ResolveSingleAsync_WhenLoopbackAddress_ReturnsLocalSourceImmediately()
    {
        // Arrange
        var resolver = new OfflineDnsResolver();

        // Act
        var entry = await resolver.ResolveSingleAsync(IPAddress.Loopback);

        // Assert
        entry.HostName.Should().Be("127.0.0.1");
        entry.Source.Should().Be("Local");
    }

    [Fact]
    public async Task ResolveSingleAsync_WhenUnspecifiedIpv4Address_ReturnsExcludedSourceImmediatelyWithoutCallingDns()
    {
        // Arrange
        var resolver = new OfflineDnsResolver();

        // Act
        var entry = await resolver.ResolveSingleAsync(IPAddress.Any);

        // Assert
        entry.HostName.Should().BeEmpty();
        entry.Source.Should().Be("Excluded");
    }

    [Fact]
    public async Task ResolveSingleAsync_WhenUnspecifiedIpv6Address_ReturnsExcludedSourceImmediatelyWithoutCallingDns()
    {
        // Arrange
        var resolver = new OfflineDnsResolver();

        // Act
        var entry = await resolver.ResolveSingleAsync(IPAddress.IPv6Any);

        // Assert
        entry.HostName.Should().BeEmpty();
        entry.Source.Should().Be("Excluded");
    }

    [Fact]
    public async Task ResolveSingleAsync_WhenBroadcastAddress_ReturnsExcludedSourceImmediatelyWithoutCallingDns()
    {
        // Arrange
        var resolver = new OfflineDnsResolver();

        // Act
        var entry = await resolver.ResolveSingleAsync(IPAddress.None);

        // Assert
        entry.HostName.Should().BeEmpty();
        entry.Source.Should().Be("Excluded");
    }

    [Fact]
    public async Task ResolveSingleAsync_WhenRdapReturnsOrganization_PopulatesEntryWithRdapSource()
    {
        // Arrange
        var targetIp = IPAddress.Parse("52.123.242.181");
        var metadata = new IpOwnerMetadata(
            Organization: "Microsoft Corporation",
            Country: "US",
            NetworkRange: "52.112.0.0 - 52.127.255.255",
            Handle: "NET-52-112-0-0-1");

        var stubOrgResolver = new StubIpOrganizationResolver(targetIp, metadata);

        var loggedMessages = new List<string>();
        var resolver = new OfflineDnsResolver(
            organizationResolver: stubOrgResolver,
            rdapThrottleDelay: TimeSpan.FromMilliseconds(10),
            logThrottlingNotice: loggedMessages.Add);

        // Act
        var entry = await resolver.ResolveSingleAsync(targetIp);

        // Assert
        entry.Organization.Should().Be("Microsoft Corporation");
        entry.Country.Should().Be("US");
        loggedMessages.Should().Contain(msg => msg.Contains("rate limiting"));
    }

    [Fact]
    public async Task ResolveSingleAsync_WhenPublicIpWithDnsHostName_AlsoQueriesRdapToEnrichOrganization()
    {
        // Arrange: 8.8.8.8 has a well-known reverse DNS PTR ("dns.google")
        var targetIp = IPAddress.Parse("8.8.8.8");
        var metadata = new IpOwnerMetadata(
            Organization: "Google LLC",
            Country: "US",
            NetworkRange: "8.8.8.0/24",
            Handle: "GOGL");

        var stubOrgResolver = new StubIpOrganizationResolver(targetIp, metadata);
        var resolver = new OfflineDnsResolver(
            organizationResolver: stubOrgResolver,
            rdapThrottleDelay: TimeSpan.FromMilliseconds(5));

        // Act
        var entry = await resolver.ResolveSingleAsync(targetIp);

        // Assert
        entry.Organization.Should().Be("Google LLC");
        entry.Country.Should().Be("US");
        entry.NetworkRange.Should().Be("8.8.8.0/24");
        stubOrgResolver.CallCount.Should().Be(1);
    }

    private sealed class StubIpOrganizationResolver(IPAddress expectedIp, IpOwnerMetadata metadata) : IIpOrganizationResolver
    {
        public int CallCount { get; private set; }

        public Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(address.Equals(expectedIp) ? metadata.Organization : null);
        }

        public Task<IpOwnerMetadata?> ResolveOwnerDetailsAsync(IPAddress address, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(address.Equals(expectedIp) ? metadata : null);
        }

        public void Dispose()
        {
        }
    }
}
