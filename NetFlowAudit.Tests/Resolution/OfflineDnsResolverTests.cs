using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

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
        loggedMessages.Should().Contain(msg => msg.Contains("Régulation"));
    }

    private sealed class StubIpOrganizationResolver(IPAddress expectedIp, IpOwnerMetadata metadata) : IIpOrganizationResolver
    {
        public Task<string?> ResolveOrganizationAsync(IPAddress address, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(address.Equals(expectedIp) ? metadata.Organization : null);
        }

        public Task<IpOwnerMetadata?> ResolveOwnerDetailsAsync(IPAddress address, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(address.Equals(expectedIp) ? metadata : null);
        }

        public void Dispose()
        {
        }
    }
}
