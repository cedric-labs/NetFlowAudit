using AwesomeAssertions;
using NetFlowAudit.Core.Export;
using Xunit;

namespace NetFlowAudit.Tests.Export;

public sealed class AuditCsvColumnCatalogTests
{
    [Fact]
    public void GetAllDescriptors_WhenInvoked_ReturnsAll20ColumnsWithCompleteMetadata()
    {
        // Act
        var descriptors = AuditCsvColumnCatalog.GetAllDescriptors();

        // Assert
        descriptors.Should().HaveCount(20);
        foreach (var desc in descriptors)
        {
            desc.Name.Should().NotBeNullOrWhiteSpace();
            desc.Category.Should().NotBeNullOrWhiteSpace();
            desc.Description.Should().NotBeNullOrWhiteSpace();
            desc.Example.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void ResolveColumns_WhenRequestedListIsNull_ReturnsDefault20Columns()
    {
        // Act
        var columns = AuditCsvColumnCatalog.ResolveColumns(null);

        // Assert
        columns.Should().HaveCount(20);
        columns.Should().Contain(AuditCsvColumn.RemoteHostName);
    }

    [Fact]
    public void ResolveColumns_WhenRequestedListIsEmpty_ReturnsDefault20Columns()
    {
        // Act
        var columns = AuditCsvColumnCatalog.ResolveColumns([]);

        // Assert
        columns.Should().HaveCount(20);
    }

    [Fact]
    public void ResolveColumns_WhenValidSubsetRequested_ReturnsExactColumnsInOrder()
    {
        // Arrange
        var requested = new[] { "LocalIp", "RemoteIp", "RemoteHostName", "ProcessName" };

        // Act
        var columns = AuditCsvColumnCatalog.ResolveColumns(requested);

        // Assert
        columns.Should().Equal(
            AuditCsvColumn.LocalIp,
            AuditCsvColumn.RemoteIp,
            AuditCsvColumn.RemoteHostName,
            AuditCsvColumn.ProcessName);
    }

    [Fact]
    public void ResolveColumns_WhenNamesHaveDifferentCase_ResolvesCaseInsensitively()
    {
        // Arrange
        var requested = new[] { "firstseenutc", "REMOTEHOSTNAME", "processid" };

        // Act
        var columns = AuditCsvColumnCatalog.ResolveColumns(requested);

        // Assert
        columns.Should().Equal(
            AuditCsvColumn.FirstSeenUtc,
            AuditCsvColumn.RemoteHostName,
            AuditCsvColumn.ProcessId);
    }

    [Fact]
    public void ResolveColumns_WhenUnknownColumnNameProvided_ThrowsArgumentException()
    {
        // Arrange
        var requested = new[] { "LocalIp", "NonExistentColumnName" };

        // Act
        var act = () => AuditCsvColumnCatalog.ResolveColumns(requested);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*NonExistentColumnName*");
    }
}
