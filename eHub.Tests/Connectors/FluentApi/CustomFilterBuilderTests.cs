using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class CustomFilterBuilderTests
{
    private readonly CustomFilterBuilder _builder = new();

    [TestMethod]
    public void AddCustomFilter_WithValidInput_AddsFilter()
    {
        var filterKey = "TestFilter";
        var sqlExpression = "CAST(BinaryData AS INT)";

        _builder.AddCustomFilter<int>(filterKey, sqlExpression);
        var result = _builder.GetCustomFilters();

        result.Should().ContainKey(filterKey);
        result[filterKey].SqlExpression.Should().Be(sqlExpression);
        result[filterKey].Type.Should().Be<int>();
    }

    [TestMethod]
    public void AddCustomFilter_WithNullFilterKey_ThrowsArgumentException()
    {
        var sqlExpression = "CAST(BinaryData AS INT)";

        var act = () => _builder.AddCustomFilter<int>(null!, sqlExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Filter key cannot be null or whitespace.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void AddCustomFilter_WithWhitespaceFilterKey_ThrowsArgumentException(string filterKey)
    {
        var sqlExpression = "CAST(BinaryData AS INT)";

        var act = () => _builder.AddCustomFilter<int>(filterKey, sqlExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage("Filter key cannot be null or whitespace.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [TestMethod]
    public void AddCustomFilter_WithPacketFilterKey_ThrowsArgumentException()
    {
        var sqlExpression = "CAST(BinaryData AS INT)";

        var act = () => _builder.AddCustomFilter<int>("Id", sqlExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"The filter key 'Id' is reserved as it matches an existing packet column.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [TestMethod]
    public void AddCustomFilter_WithNullSqlExpression_ThrowsArgumentException()
    {
        var filterKey = "TestFilter";

        var act = () => _builder.AddCustomFilter<int>(filterKey, null!);

        act.Should().Throw<ArgumentException>()
            .WithMessage("SQL expression cannot be null or whitespace.*")
            .And.ParamName.Should().Be("sqlExpression");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("   ")]
    public void AddCustomFilter_WithWhitespaceSqlExpression_ThrowsArgumentException(string sqlExpression)
    {
        var filterKey = "TestFilter";

        Action act = () => _builder.AddCustomFilter<int>(filterKey, sqlExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage("SQL expression cannot be null or whitespace.*")
            .And.ParamName.Should().Be("sqlExpression");
    }

    [TestMethod]
    public void AddCustomFilter_WithDuplicateKey_ThrowsArgumentException()
    {
        var filterKey = "DuplicateFilter";
        var sqlExpression = "CAST(BinaryData AS INT)";

        _builder.AddCustomFilter<int>(filterKey, sqlExpression);
        var act = () => _builder.AddCustomFilter<int>(filterKey, sqlExpression);

        act.Should().Throw<ArgumentException>()
            .WithMessage($"A filter with the key '{filterKey}' already exists.*")
            .And.ParamName.Should().Be("filterKey");
    }

    [TestMethod]
    public void GetCustomFilters_ReturnsImmutableDictionary_WithAllAddedFilters()
    {
        _builder
            .AddCustomFilter<int>("Filter1", "CAST(BinaryData AS INT)")
            .AddCustomFilter<string>("Filter2", "SUBSTRING(BinaryData, 1, 5)")
            .AddCustomFilter<DateTime>("Filter3", "CAST(BinaryData AS DATETIME)");

        var filters = _builder.GetCustomFilters();

        filters.Should().HaveCount(3);
        filters.Should().ContainKey("Filter1");
        filters.Should().ContainKey("Filter2");
        filters.Should().ContainKey("Filter3");

        filters["Filter1"].SqlExpression.Should().Be("CAST(BinaryData AS INT)");
        filters["Filter1"].Type.Should().Be<int>();

        filters["Filter2"].SqlExpression.Should().Be("SUBSTRING(BinaryData, 1, 5)");
        filters["Filter2"].Type.Should().Be<string>();

        filters["Filter3"].SqlExpression.Should().Be("CAST(BinaryData AS DATETIME)");
        filters["Filter3"].Type.Should().Be<DateTime>();
    }
}
