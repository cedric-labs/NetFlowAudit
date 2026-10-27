using AwesomeAssertions;
using NetFlowAudit.Core.Resolution;
using Xunit;

namespace NetFlowAudit.Tests.Resolution;

public sealed class RdapResponseParserTests
{
    [Fact]
    public void ExtractOrganization_WhenJsonHasRegistrantVcard_ReturnsRegistrantName()
    {
        // Arrange
        var json = """
        {
          "name": "MSFT",
          "entities": [
            {
              "roles": [ "technical" ],
              "vcardArray": [ "vcard", [ [ "fn", {}, "text", "Prachi Singh" ] ] ]
            },
            {
              "roles": [ "registrant" ],
              "vcardArray": [ "vcard", [ [ "fn", {}, "text", "Microsoft Corporation" ] ] ]
            }
          ]
        }
        """;

        // Act
        var result = RdapResponseParser.ExtractOrganization(json);

        // Assert
        result.Should().Be("Microsoft Corporation");
    }

    [Fact]
    public void ExtractOwnerDetails_WhenJsonHasFullDetails_ReturnsCompleteMetadata()
    {
        // Arrange
        var json = """
        {
          "name": "MSFT",
          "handle": "NET-52-112-0-0-1",
          "startAddress": "52.112.0.0",
          "endAddress": "52.127.255.255",
          "country": "US",
          "entities": [
            {
              "roles": [ "registrant" ],
              "vcardArray": [ "vcard", [ [ "fn", {}, "text", "Microsoft Corporation" ] ] ]
            }
          ]
        }
        """;

        // Act
        var details = RdapResponseParser.ExtractOwnerDetails(json);

        // Assert
        details.Should().NotBeNull();
        details!.Organization.Should().Be("Microsoft Corporation");
        details.Country.Should().Be("US");
        details.NetworkRange.Should().Be("52.112.0.0 - 52.127.255.255");
        details.Handle.Should().Be("NET-52-112-0-0-1");
    }

    [Fact]
    public void ExtractOrganization_WhenJsonHasOnlyNameProperty_ReturnsName()
    {
        // Arrange
        var json = """
        {
          "name": "CLOUDFLARE-NET",
          "entities": []
        }
        """;

        // Act
        var result = RdapResponseParser.ExtractOrganization(json);

        // Assert
        result.Should().Be("CLOUDFLARE-NET");
    }

    [Fact]
    public void ExtractOrganization_WhenJsonHasOrgPropertyInVcard_ReturnsOrgName()
    {
        // Arrange
        var json = """
        {
          "entities": [
            {
              "roles": [ "administrative" ],
              "vcardArray": [
                "vcard",
                [
                  [ "fn", {}, "text", "John Doe" ],
                  [ "org", {}, "text", "Acme Corporation" ]
                ]
              ]
            }
          ]
        }
        """;

        // Act
        var result = RdapResponseParser.ExtractOrganization(json);

        // Assert
        result.Should().Be("Acme Corporation");
    }

    [Fact]
    public void ExtractOrganization_WhenJsonHasNestedEntities_ReturnsNestedRegistrant()
    {
        // Arrange
        var json = """
        {
          "entities": [
            {
              "roles": [ "noc" ],
              "entities": [
                {
                  "roles": [ "registrant" ],
                  "vcardArray": [ "vcard", [ [ "fn", {}, "text", "Google LLC" ] ] ]
                }
              ]
            }
          ]
        }
        """;

        // Act
        var result = RdapResponseParser.ExtractOrganization(json);

        // Assert
        result.Should().Be("Google LLC");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not valid json")]
    [InlineData("{}")]
    public void ExtractOrganization_WhenJsonIsNullOrEmptyOrInvalid_ReturnsNull(string? invalidJson)
    {
        // Act
        var result = invalidJson is not null
            ? RdapResponseParser.ExtractOrganization(invalidJson)
            : RdapResponseParser.ExtractOrganization(string.Empty);

        // Assert
        result.Should().BeNull();
    }
}
