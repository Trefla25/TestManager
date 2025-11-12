using eHub.Contracts.UIConfig;
using eHub.Contracts;
using eHub.PlugIn.UI;
using eHub.PlugIn;
using System.Globalization;
using eHub.UI.Models;
using eHub.UI.Services;
using eHub.UI.Util;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using MudBlazor;
using Microsoft.AspNetCore.WebUtilities;

namespace eHub.UI.Tests.Services;

public class HistoryStateUrlServiceTests
{
    private const string BaseAddress = "http://testdomain/eController/IntegrationHub/UI/Pages/History";
    private readonly IConnectorRegistry _connectorRegistry;
    private readonly HistoryStateUrlService _historyStateUrlService;
    
    public HistoryStateUrlServiceTests()
    {
        var logger = Substitute.For<ILogger<HistoryStateUrlService>>();
        _connectorRegistry = Substitute.For<IConnectorRegistry>();
        _historyStateUrlService = new HistoryStateUrlService(logger, _connectorRegistry);
    }

    [Fact]
    public void GetStateFromUrl_WithValidConnector_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?connector=MyConnector";

        var connectorIdentifier = new ConnectorIdentifier("Instance", "MyConnector");
        var otherIdentifier = new ConnectorIdentifier("Instance", "OtherConnector");

        _connectorRegistry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
        {
            { connectorIdentifier, new ConnectorUiData(connectorIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) },
            { otherIdentifier, new ConnectorUiData(otherIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) }
        });
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.Connector.Should().Be(connectorIdentifier);
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidConnector_ReturnsNull()
    {
        // Arrange
        const string url = BaseAddress + "?connector=InvalidConnector";

        var connectorIdentifier = new ConnectorIdentifier("Instance", "MyConnector");
        var otherIdentifier = new ConnectorIdentifier("Instance", "OtherConnector");

        _connectorRegistry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
        {
            { connectorIdentifier, new ConnectorUiData(connectorIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) },
            { otherIdentifier, new ConnectorUiData(otherIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) }
        });
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.Connector.Should().BeNull();
    }

    [Fact]
    public void GetStateFromUrl_WithValidViewIndex_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?view=2";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.View.Should().Be(2);
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidViewIndex_ReturnsDefault()
    {
        // Arrange
        const string url = BaseAddress + "?view=invalid";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.View.Should().Be(0);
    }

    [Fact]
    public void GetStateFromUrl_WithValidLiveMode_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?liveMode=false";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.LiveMode.Should().BeFalse();
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidLiveMode_ReturnsDefault()
    {
        // Arrange
        const string url = BaseAddress + "?liveMode=invalid";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.LiveMode.Should().BeTrue();
    }

    [Fact]
    public void GetStateFromUrl_WithValidStartDate_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?startDate=10-03-2026";
        var expectedStart = DateTime.ParseExact("10-03-2026", "dd-MM-yyyy", CultureInfo.InvariantCulture);
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.StartDateTime.Should().Be(expectedStart);
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidStartDate_ReturnsNull()
    {
        // Arrange
        const string url = BaseAddress + "?startDate=3/10/2026";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.StartDateTime.Should().BeNull();
    }

    [Fact]
    public void GetStateFromUrl_WithValidStartTime_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?startDate=10-03-2026&startTime=10-11";
        var expectedStart = DateTime.ParseExact("10-03-2026 10:11", "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.StartDateTime.Should().Be(expectedStart);
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidStartTime_ReturnsDefault()
    {
        // Arrange
        const string url = BaseAddress + "?startDate=10-03-2026&startTime=00-99";
        var expectedStart = DateTime.ParseExact("10-03-2026", "dd-MM-yyyy", CultureInfo.InvariantCulture);
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.StartDateTime.Should().Be(expectedStart);
    }

    [Fact]
    public void GetStateFromUrl_WithValidEndDate_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?endDate=11-03-2027";
        var expectedEnd = DateTime.ParseExact("11-03-2027", "dd-MM-yyyy", CultureInfo.InvariantCulture);
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.EndDateTime.Should().Be(expectedEnd);
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidEndDate_ReturnsNull()
    {
        // Arrange
        const string url = BaseAddress + "?endDate=3/11/2027";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.EndDateTime.Should().BeNull();
    }

    [Fact]
    public void GetStateFromUrl_WithValidEndTime_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?endDate=11-03-2027&endTime=11-12";
        var expectedEnd = DateTime.ParseExact("11-03-2027 11:12", "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.EndDateTime.Should().Be(expectedEnd);
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidEndTime_ReturnsDefault()
    {
        // Arrange
        const string url = BaseAddress + "?endDate=11-03-2027&endTime=00-99";
        var expectedEnd = DateTime.ParseExact("11-03-2027", "dd-MM-yyyy", CultureInfo.InvariantCulture);
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.EndDateTime.Should().Be(expectedEnd);
    }

    [Fact]
    public void GetStateFromUrl_WithValidColumnFilter_ParsesValue()
    {
        // Arrange
        const string url = BaseAddress + "?column=Channel-contains-SomeChannel";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.ColumnFilters.Should().NotBeEmpty();
        state.ColumnFilters[0].ColumnName.Should().Be(nameof(PacketDto.Channel));
        state.ColumnFilters[0].Operator.Should().Be(FilterOperator.String.Contains);
        state.ColumnFilters[0].Value.Should().Be("SomeChannel");
    }

    [Fact]
    public void GetStateFromUrl_WithMultipleColumnFilters_ParsesValues()
    {
        // Arrange
        const string url = BaseAddress +
            "?column=channel-contains-SomeChannel" +
            "&column=STATUS-is-Processed" +
            "&column=ParentId-%3E%3D-20";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.ColumnFilters.Should().HaveCount(3);

        state.ColumnFilters[0].ColumnName.Should().Be(nameof(PacketDto.Channel));
        state.ColumnFilters[0].Operator.Should().Be(FilterOperator.String.Contains);
        state.ColumnFilters[0].Value.Should().Be("SomeChannel");

        state.ColumnFilters[1].ColumnName.Should().Be(nameof(PacketDto.Status));
        state.ColumnFilters[1].Operator.Should().Be(FilterOperator.Enum.Is);
        state.ColumnFilters[1].Value.Should().Be(nameof(PacketStatus.Processed));

        state.ColumnFilters[2].ColumnName.Should().Be(nameof(PacketDto.ParentId));
        state.ColumnFilters[2].Operator.Should().Be(FilterOperator.Number.GreaterThanOrEqual);
        state.ColumnFilters[2].Value.Should().Be("20");
    }

    [Fact]
    public void GetStateFromUrl_WithInvalidColumnFilters_SkipsInvalid()
    {
        // Arrange
        const string url = BaseAddress +
            "?column= " +
            "&column=invalid-format" +
            "&column=InvalidColumn-equals-data" +
            "&column=Id-contains-10" +
            "&column=Data-contains-valid";
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.ColumnFilters.Should().HaveCount(1);
        state.ColumnFilters[0].ColumnName.Should().Be(nameof(PacketDto.Data));
        state.ColumnFilters[0].Operator.Should().Be(FilterOperator.String.Contains);
        state.ColumnFilters[0].Value.Should().Be("valid");
    }

    [Fact]
    public void GetStateFromUrl_WithAllParameters_ParsesAll()
    {
        // Arrange
        const string url = BaseAddress +
            "?connector=MyConnector" +
            "&view=1" +
            "&liveMode=true" +
            "&startDate=10-03-2026" +
            "&startTime=10-11" +
            "&endDate=11-03-2026" +
            "&endTime=11-12" +
            "&column=Channel-contains-SomeChannel";

        var connectorIdentifier = new ConnectorIdentifier("Instance", "MyConnector");
        var otherIdentifier = new ConnectorIdentifier("Instance", "OtherConnector");

        _connectorRegistry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
        {
            { connectorIdentifier, new ConnectorUiData("MyConnector", "MyConnectorType", new UIViewConfig()) },
            { otherIdentifier, new ConnectorUiData("OtherConnector", "MyConnectorType", new UIViewConfig()) }
        });
       
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);

        var expectedStart = DateTime.ParseExact("10-03-2026 10-11", "dd-MM-yyyy HH-mm", CultureInfo.InvariantCulture);
        var expectedEnd = DateTime.ParseExact("11-03-2026 11-12", "dd-MM-yyyy HH-mm", CultureInfo.InvariantCulture);
        
        // Assert
        state.Connector.Should().Be(connectorIdentifier);
        state.View.Should().Be(1);
        state.LiveMode.Should().BeTrue();
        state.StartDateTime.Should().Be(expectedStart);
        state.EndDateTime.Should().Be(expectedEnd);
        state.ColumnFilters.Should().NotBeEmpty();
        state.ColumnFilters[0].ColumnName.Should().Be("Channel");
        state.ColumnFilters[0].Operator.Should().Be(FilterOperator.String.Contains);
        state.ColumnFilters[0].Value.Should().Be("SomeChannel");
    }

    [Fact]
    public void GetStateFromUrl_AllColumnOperators_ParsesAll()
    {
        // Arrange
        var expectedCount = 0;
        var queryString = string.Empty;
        foreach (var column in PacketDto.PacketColumns)
        {
            var columnType = typeof(PacketDto).GetProperty(column)?.PropertyType;
            var fieldType = FieldType.Identify(columnType);
            var validOperators = FilterOperatorUtil.GetOperators(fieldType);

            foreach (var op in validOperators)
            {
                queryString += $"&Column={Uri.EscapeDataString($"{column}-{op}-test")}";
                expectedCount++;
            }
        }

        queryString = queryString.TrimStart('&');
        var url = BaseAddress + '?' + queryString;
        
        // Act
        var state = _historyStateUrlService.GetStateFromUrl(url);
        
        // Assert
        state.ColumnFilters.Should().HaveCount(expectedCount);
    }

    [Fact]
    public void AppendStateQuery_WithConnector_AppendsExpectedQuery()
    {
        // Arrange
        var connector = new ConnectorIdentifier("Instance", "MyConnector");
        var state = new HistoryState
        {
            Connector = connector
        };

        // Act
        var resultUrl = HistoryStateUrlService.AppendStateQuery(BaseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Keys.Should().Contain("Connector");
        queryParams["Connector"].ToString().Should().Be("MyConnector");
    }

    [Fact]
    public void AppendStateQuery_WithView_AppendsExpectedQuery()
    {
        // Arrange
        var state = new HistoryState
        {
            View = 3,
        };

        // Act
        var resultUrl = HistoryStateUrlService.AppendStateQuery(BaseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Keys.Should().Contain("View");
        queryParams["View"].ToString().Should().Be("3");
    }

    [Fact]
    public void AppendStateQuery_WithLiveMod_AppendsExpectedQuery()
    {
        // Arrange
        var state = new HistoryState
        {
            LiveMode = false
        };

        // Act
        var resultUrl = HistoryStateUrlService.AppendStateQuery(BaseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Keys.Should().Contain("LiveMode");
        queryParams["LiveMode"].ToString().Should().Be("False");
    }

    [Fact]
    public void AppendStateQuery_WithStartDateTime_AppendsExpectedQuery()
    {
        // Arrange
        var startDateTime = DateTime.Now;
        var expectedStartDate = startDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var expectedStartTime = startDateTime.ToString("HH-mm", CultureInfo.InvariantCulture);

        var state = new HistoryState
        {
            StartDateTime = startDateTime,
        };

        // Act
        var resultUrl = HistoryStateUrlService.AppendStateQuery(BaseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Keys.Should().Contain(["StartDate", "StartTime"]);
        queryParams["StartDate"].ToString().Should().Be(expectedStartDate);
        queryParams["StartTime"].ToString().Should().Be(expectedStartTime);
    }

    [Fact]
    public void AppendStateQuery_WithEndDateTime_AppendsExpectedQuery()
    {
        // Arrange
        var endDateTime = DateTime.Now;
        var expectedEndDate = endDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var expectedEndTime = endDateTime.ToString("HH-mm", CultureInfo.InvariantCulture);

        // Act
        var state = new HistoryState
        {
            EndDateTime = endDateTime,
        };

        var resultUrl = HistoryStateUrlService.AppendStateQuery(BaseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Keys.Should().Contain(["EndDate", "EndTime"]);
        queryParams["EndDate"].ToString().Should().Be(expectedEndDate);
        queryParams["EndTime"].ToString().Should().Be(expectedEndTime);
    }

    [Fact]
    public void AppendStateQuery_WithColumnFilters_AppendsExpectedQuery()
    {
        // Arrange
        var state = new HistoryState
        {
            ColumnFilters =
            [
                new PacketColumnFilter
                {
                    ColumnName = nameof(PacketDto.Id),
                    Operator = FilterOperator.Number.GreaterThan,
                    Value = "5"
                },
                new PacketColumnFilter
                {
                    ColumnName = nameof(PacketDto.Channel),
                    Operator = FilterOperator.String.EndsWith,
                    Value = "Channel"
                },
                new PacketColumnFilter
                {
                    ColumnName = nameof(PacketDto.DateChanged),
                    Operator = FilterOperator.DateTime.OnOrBefore,
                    Value = DateTime.Now.ToString("dd-MM-yyyy HH:mm")
                }
            ]
        };

        // Act
        var resultUrl = HistoryStateUrlService.AppendStateQuery(BaseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Keys.Should().Contain("Column");
        var columnValues = queryParams["Column"];
        columnValues.Should().Contain($"{state.ColumnFilters[0].ColumnName}-{state.ColumnFilters[0].Operator}-{state.ColumnFilters[0].Value}");
        columnValues.Should().Contain($"{state.ColumnFilters[1].ColumnName}-{state.ColumnFilters[1].Operator}-{state.ColumnFilters[1].Value}");
        columnValues.Should().Contain($"{state.ColumnFilters[2].ColumnName}-{state.ColumnFilters[2].Operator}-{state.ColumnFilters[2].Value}");
    }

    [Fact]
    public void AppendStateQuery_WhenUrlHasQuery_ReplacesQuery()
    {
        // Arrange
        var state = new HistoryState
        {
            Connector = new ConnectorIdentifier("Instance", "MyConnector")
        };

        // Act
        const string urlWithQuery = BaseAddress + "?existingParam=existingValue";
        var resultUrl = HistoryStateUrlService.AppendStateQuery(urlWithQuery, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Keys.Should().NotContain("existingParam");
        queryParams.Keys.Should().Contain("Connector");
        queryParams["Connector"].ToString().Should().Be("MyConnector");
    }

    [Fact]
    public void AppendStateQuery_WithAllParameters_AppendsExpectedQuery()
    {
        // Arrange
        var dateTimeStart = DateTime.Now;
        var dateTimeEnd = DateTime.Now.AddDays(1).AddHours(1);

        var expectedStartDate = dateTimeStart.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var expectedStartTime = dateTimeStart.ToString("HH-mm", CultureInfo.InvariantCulture);
        var expectedEndDate = dateTimeEnd.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var expectedEndTime = dateTimeEnd.ToString("HH-mm", CultureInfo.InvariantCulture);

        var state = new HistoryState
        {
            Connector = new ConnectorIdentifier("Instance", "MyConnector"),
            View = 2,
            LiveMode = false,
            StartDateTime = dateTimeStart,
            EndDateTime = dateTimeEnd,
            ColumnFilters =
            [
                new PacketColumnFilter
                {
                    ColumnName = nameof(PacketDto.Channel),
                    Operator = FilterOperator.String.Contains,
                    Value = "SomeChannel"
                },
                new PacketColumnFilter
                {
                    ColumnName = nameof(PacketDto.Status),
                    Operator = FilterOperator.Enum.Is,
                    Value = nameof(PacketStatus.Processed)
                }
            ]
        };

        // Act
        var resultUrl = HistoryStateUrlService.AppendStateQuery(BaseAddress, state);

        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);
        
        // Assert
        queryParams.Should().ContainKey("Connector");
        queryParams["Connector"].Should().Contain("MyConnector");

        queryParams.Should().ContainKey("View");
        queryParams["View"].ToString().Should().Be("2");

        queryParams.Should().ContainKey("LiveMode");
        queryParams["LiveMode"].ToString().Should().Be("False");

        queryParams.Should().ContainKey("StartDate");
        queryParams["StartDate"].ToString().Should().Be(expectedStartDate);

        queryParams.Should().ContainKey("StartTime");
        queryParams["StartTime"].ToString().Should().Be(expectedStartTime);

        queryParams.Should().ContainKey("EndDate");
        queryParams["EndDate"].ToString().Should().Be(expectedEndDate);

        queryParams.Should().ContainKey("EndTime");
        queryParams["EndTime"].ToString().Should().Be(expectedEndTime);

        queryParams.Should().ContainKey("Column");
        var columnValues = queryParams["Column"];
        columnValues.Should().Contain($"{state.ColumnFilters[0].ColumnName}-{state.ColumnFilters[0].Operator}-{state.ColumnFilters[0].Value}");
        columnValues.Should().Contain($"{state.ColumnFilters[1].ColumnName}-{state.ColumnFilters[1].Operator}-{state.ColumnFilters[1].Value}");
    }
}
