using System.Net;
using FluentAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

public sealed class RdapIpOrganizationResolverTests
{
    private sealed class DelegatingTestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task ResolveOrganizationAsync_WhenAddressIsNull_ThrowsArgumentNullException()
    {
        // Arrange
        using var resolver = new RdapIpOrganizationResolver();

        // Act
        var act = () => resolver.ResolveOrganizationAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.1.25")]
    [InlineData("192.168.1.1")]
    [InlineData("172.16.5.10")]
    [InlineData("169.254.1.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    public async Task ResolveOrganizationAsync_WhenAddressIsPrivateOrLoopback_ReturnsNullImmediatelyWithoutHttpCall(string ipString)
    {
        // Arrange
        var httpCalled = false;
        var handler = new DelegatingTestHandler(_ =>
        {
            httpCalled = true;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var client = new HttpClient(handler);
        using var resolver = new RdapIpOrganizationResolver(client);
        var ip = IPAddress.Parse(ipString);

        // Act
        var result = await resolver.ResolveOrganizationAsync(ip);

        // Assert
        result.Should().BeNull();
        httpCalled.Should().BeFalse();
    }

    [Fact]
    public async Task ResolveOrganizationAsync_WhenHttpReturnsValidRdapJson_ReturnsExtractedOrganization()
    {
        // Arrange
        var validJson = """
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

        var handler = new DelegatingTestHandler(req =>
        {
            req.RequestUri!.ToString().Should().Contain("52.123.242.181");
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(validJson)
            };
        });

        using var client = new HttpClient(handler);
        using var resolver = new RdapIpOrganizationResolver(client);
        var ip = IPAddress.Parse("52.123.242.181");

        // Act
        var result = await resolver.ResolveOrganizationAsync(ip);

        // Assert
        result.Should().Be("Microsoft Corporation");
    }

    [Fact]
    public async Task ResolveOrganizationAsync_WhenHttpThrowsNetworkException_ReturnsNullAndIncrementsFailureCount()
    {
        // Arrange
        var handler = new DelegatingTestHandler(_ => throw new HttpRequestException("Network unreachable in air-gapped server"));

        using var client = new HttpClient(handler);
        using var resolver = new RdapIpOrganizationResolver(client, circuitBreakerThreshold: 2);
        var ip = IPAddress.Parse("52.123.242.181");

        // Act
        var result = await resolver.ResolveOrganizationAsync(ip);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task ResolveOrganizationAsync_WhenCircuitBreakerTrips_ReturnsNullImmediatelyWithoutHttpCall()
    {
        // Arrange: Threshold set to 2 consecutive errors
        var httpCallCount = 0;
        var handler = new DelegatingTestHandler(_ =>
        {
            httpCallCount++;
            throw new HttpRequestException("Connection refused (offline)");
        });

        using var client = new HttpClient(handler);
        using var resolver = new RdapIpOrganizationResolver(client, circuitBreakerThreshold: 2, circuitBreakerCooldown: TimeSpan.FromMinutes(5));
        var ip1 = IPAddress.Parse("52.123.242.181");
        var ip2 = IPAddress.Parse("52.123.242.182");
        var ip3 = IPAddress.Parse("52.123.242.183");

        // Act
        await resolver.ResolveOrganizationAsync(ip1);
        await resolver.ResolveOrganizationAsync(ip2);
        var result3 = await resolver.ResolveOrganizationAsync(ip3);

        // Assert
        result3.Should().BeNull();
        httpCallCount.Should().Be(2);
    }
}
