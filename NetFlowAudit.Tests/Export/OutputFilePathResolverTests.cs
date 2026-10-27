using AwesomeAssertions;
using NetFlowAudit.Core.Export;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class OutputFilePathResolverTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_WhenTemplateIsNullOrWhiteSpace_ReturnsTemplateUnchanged(string? template)
    {
        var resolved = OutputFilePathResolver.Resolve(template!);
        resolved.Should().Be(template);
    }

    [Fact]
    public void Resolve_WhenTemplateContainsNoTokens_ReturnsTemplateUnchanged()
    {
        const string template = "audit/output_file.csv";
        var resolved = OutputFilePathResolver.Resolve(template);
        resolved.Should().Be(template);
    }

    [Fact]
    public void Resolve_WhenTemplateContainsServerName_ReplacesWithMachineName()
    {
        const string template = "audit_{ServerName}.csv";
        var resolved = OutputFilePathResolver.Resolve(template, machineName: "SRV-APP-01");
        resolved.Should().Be("audit_SRV-APP-01.csv");
    }

    [Fact]
    public void Resolve_WhenTemplateContainsHostName_ReplacesWithHostName()
    {
        const string template = "logs/{HostName}_report.csv";
        var resolved = OutputFilePathResolver.Resolve(template, machineName: "SRV-DB-02");
        resolved.Should().Be("logs/SRV-DB-02_report.csv");
    }

    [Fact]
    public void Resolve_WhenTemplateContainsDate_ReplacesWithFormattedDate()
    {
        const string template = "audit_{Date}.csv";
        var fixedDate = new DateTime(2026, 10, 24, 14, 30, 45, DateTimeKind.Utc);
        var resolved = OutputFilePathResolver.Resolve(template, nowUtc: fixedDate);
        resolved.Should().Be("audit_2026-10-24.csv");
    }

    [Fact]
    public void Resolve_WhenTemplateContainsTimestamp_ReplacesWithFormattedTimestamp()
    {
        const string template = "audit_{Timestamp}.csv";
        var fixedDate = new DateTime(2026, 10, 24, 14, 30, 45, DateTimeKind.Utc);
        var resolved = OutputFilePathResolver.Resolve(template, nowUtc: fixedDate);
        resolved.Should().Be("audit_20261024_143045.csv");
    }

    [Fact]
    public void Resolve_WhenTokenContainsInvalidFileNameCharacters_SanitizesInvalidCharacters()
    {
        const string template = "audit_{ServerName}.csv";
        // Invalid chars like : / \ * ? " < > |
        var resolved = OutputFilePathResolver.Resolve(template, machineName: "SRV:APP/01*TEST");
        resolved.Should().Be("audit_SRV-APP-01-TEST.csv");
    }

    [Fact]
    public void Resolve_WhenTemplateContainsDirectoryPath_PreservesDirectorySeparators()
    {
        const string template = @"C:\Logs\{ServerName}\audit_{Date}.csv";
        var fixedDate = new DateTime(2026, 10, 24, 8, 0, 0, DateTimeKind.Utc);
        var resolved = OutputFilePathResolver.Resolve(template, machineName: "SRV-01", nowUtc: fixedDate);
        resolved.Should().Be(@"C:\Logs\SRV-01\audit_2026-10-24.csv");
    }

    [Fact]
    public void Resolve_WhenTemplateContainsMultipleTokensCaseInsensitive_ResolvesAllTokens()
    {
        const string template = "export_{servername}_{date}_{TIMESTAMP}.html";
        var fixedDate = new DateTime(2026, 10, 24, 9, 15, 30, DateTimeKind.Utc);
        var resolved = OutputFilePathResolver.Resolve(template, machineName: "WEB-01", nowUtc: fixedDate);
        resolved.Should().Be("export_WEB-01_2026-10-24_20261024_091530.html");
    }
}
