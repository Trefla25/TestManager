using eHub.Contracts.UIConfig;
using eHub.Contracts;
using eHub.Contracts.Helper;
using FluentAssertions;

namespace eHub.Tests.Connectors.PacketTransfer.UI;

[TestClass]
public class ColumnFilterTests
{
    [TestMethod]
    public void IsNumericFilter_WithNumericColumns_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeTrue();
        new ColumnFilterDto (nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeTrue();
        new ColumnFilterDto (nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeTrue();
    }

    [TestMethod]
    public void IsNumericFilter_WithNonNumericColumns_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsNumericFilter().Should().BeFalse();
    }

    [TestMethod]
    public void IsDateTimeFilter_WithDateTimeColumns_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeTrue();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeTrue();
    }

    [TestMethod]
    public void IsDateTimeFilter_WithNonDateTimeColumns_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsDateTimeFilter().Should().BeFalse();
    }

    [TestMethod]
    public void IsStringFilter_WithStringColumns_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeTrue();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeTrue();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeTrue();
    }

    [TestMethod]
    public void IsStringFilter_WithNonStringColumns_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsStringFilter().Should().BeFalse();
    }

    [TestMethod]
    public void IsStatusFilter_WithStatusColumn_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeTrue();
    }

    [TestMethod]
    public void IsStatusFilter_WithNonStatusColumn_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsStatusFilter().Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFilter_WithDataColumn_ReturnsTrue()
    {
        new ColumnFilterDto(nameof(PacketDto.Data), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFilter_WithNonDataColumn_ReturnsFalse()
    {
        new ColumnFilterDto(nameof(PacketDto.Id), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.ParentId), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.RetryCount), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateCreated), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DateChanged), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Channel), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.DynamicField), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Metadata), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
        new ColumnFilterDto(nameof(PacketDto.Status), ColumnFilterOperator.Equal, null).IsDataFilter().Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_NoFilters_ReturnsTrue()
    {
        var data = "any value";
        var filters = new List<ColumnFilterDto>();

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingEqual_ReturnsTrue()
    {
        var data = "match";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Equal, "match" )
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingEqual_ReturnsFalse()
    {
        var data = "no match";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Equal, "match" )
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingNotEqual_ReturnsFalse()
    {
        var data = "match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEqual, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingNotEqual_ReturnsTrue()
    {
        var data = "no match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEqual, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingContains_ReturnsTrue()
    {
        var data = "one match data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Contains, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingContains_ReturnsFalse()
    {
        var data = "no data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Contains, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingNotContains_ReturnsTrue()
    {
        var data = "no data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotContains, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingNotContains_ReturnsFalse()
    {
        var data = "one match data";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotContains, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingStart_ReturnsTrue()
    {
        var data = "match this";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingStart_ReturnsFalse()
    {
        // Arrange
        var data = "no match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingEnd_ReturnsTrue()
    {
        var data = "this match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingEnd_ReturnsFalse()
    {
        // Arrange
        var data = "match this";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "match")
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingEmpty_ReturnsTrue()
    {
        var data = "";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Empty, null)
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingEmpty_ReturnsFalse()
    {
        var data = "not empty";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.Empty, null)
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_WhenMatchingNotEmpty_ReturnsTrue()
    {
        var data = "match";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEmpty, null)
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_WhenNotMatchingNotEmpty_ReturnsFalse()
    {
        var data = "";
        var filters = new ColumnFilterDto[]
        {
            new (nameof(PacketDto.Data), ColumnFilterOperator.NotEmpty, null)
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_MultipleValidFilters_ReturnsTrue()
    {
        var data = "hello world!";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Contains, "hello" ),
            new(nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "world!"),
            new(nameof(PacketDto.Data), ColumnFilterOperator.NotEmpty, null)
        };

        var result = filters.Matches(data);

        result.Should().BeTrue();
    }

    [TestMethod]
    public void IsDataFiltered_AtleastOneInvalidFilter_ReturnsFalse()
    {
        var data = "hello world!";
        var filters = new ColumnFilterDto[]
        {
            new(nameof(PacketDto.Data), ColumnFilterOperator.Contains, "hello" ),
            new(nameof(PacketDto.Data), ColumnFilterOperator.EndsWith, "world!"),
            new(nameof(PacketDto.Data), ColumnFilterOperator.StartsWith, "start")
        };

        var result = filters.Matches(data);

        result.Should().BeFalse();
    }

    [TestMethod]
    public void IsDataFiltered_UnsupportedOperators_ThrowsException()
    {
        var data = "some data";
        var greaterThanFilter = new ColumnFilterDto[] { new (nameof(PacketDto.Data), ColumnFilterOperator.GreaterThan, null) };
        var greaterThanOrEqualFilter = new ColumnFilterDto[] { new (nameof(PacketDto.Data), ColumnFilterOperator.GreaterThanOrEqual, null) };
        var lessThanFilter = new ColumnFilterDto[] { new(nameof(PacketDto.Data), ColumnFilterOperator.LessThan, null) };
        var lessThanOrEqualFilter = new ColumnFilterDto[] { new(nameof(PacketDto.Data), ColumnFilterOperator.LessThanOrEqual, null) };

        var greaterThanAct = () => greaterThanFilter.Matches(data);
        var greaterThanOrEqualAct = () => greaterThanOrEqualFilter.Matches(data);
        var lessThanAct = () => lessThanFilter.Matches(data);
        var lessThanOrEqualAct = () => lessThanOrEqualFilter.Matches(data);

        greaterThanAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator GreaterThan for filtering data");
        greaterThanOrEqualAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator GreaterThanOrEqual for filtering data");
        lessThanAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator LessThan for filtering data");
        lessThanOrEqualAct.Should().Throw<InvalidOperationException>().WithMessage("Unsupported operator LessThanOrEqual for filtering data");
    }
}
