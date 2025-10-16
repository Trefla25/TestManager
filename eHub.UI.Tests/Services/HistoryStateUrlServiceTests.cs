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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eHub.UI.Tests.Services;

[TestClass]
public class HistoryStateUrlServiceTests
{
    private readonly string _baseAddress = "http://testdomain/eController/IntegrationHub/UI/Pages/History";
    private ILogger<HistoryStateUrlService> _logger = default!;
    private IConnectorRegistry _connectorRegistry = default!;
    private HistoryStateUrlService _historyStateUrlService = default!;

    [TestInitialize]
    public void TestInitialize()
    {
        _logger = Substitute.For<ILogger<HistoryStateUrlService>>();
        _connectorRegistry = Substitute.For<IConnectorRegistry>();

        _historyStateUrlService = new HistoryStateUrlService(_logger, _connectorRegistry);
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidConnector_ParsesValue()
    {
        string url = _baseAddress + "?connector=MyConnector";

        var connectorIdentifier = new ConnectorIdentifier("Instance", "MyConnector");
        var otherIdentifier = new ConnectorIdentifier("Instance", "OtherConnector");

        _connectorRegistry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
        {
            { connectorIdentifier, new ConnectorUiData(connectorIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) },
            { otherIdentifier, new ConnectorUiData(otherIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) }
        });

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.Connector.Should().Be(connectorIdentifier);
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidConnector_ReturnsNull()
    {
        string url = _baseAddress + "?connector=InvalidConnector";

        var connectorIdentifier = new ConnectorIdentifier("Instance", "MyConnector");
        var otherIdentifier = new ConnectorIdentifier("Instance", "OtherConnector");

        _connectorRegistry.ActiveConnectors.Returns(new Dictionary<ConnectorIdentifier, ConnectorUiData>
        {
            { connectorIdentifier, new ConnectorUiData(connectorIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) },
            { otherIdentifier, new ConnectorUiData(otherIdentifier.ConnectorKey, "MyConnectorType", new UIViewConfig()) }
        });

        var state = _historyStateUrlService.GetStateFromUrl(url);
        state.Connector.Should().BeNull();
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidViewIndex_ParsesValue()
    {
        string url = _baseAddress + "?view=2";

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.View.Should().Be(2);
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidViewIndex_ReturnsDefault()
    {
        string url = _baseAddress + "?view=invalid";

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.View.Should().Be(0);
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidLiveMode_ParsesValue()
    {
        string url = _baseAddress + "?liveMode=false";

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.LiveMode.Should().BeFalse();
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidLiveMode_ReturnsDefault()
    {
        string url = _baseAddress + "?liveMode=invalid";

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.LiveMode.Should().BeTrue();
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidStartDate_ParsesValue()
    {
        string url = _baseAddress + "?startDate=10-03-2026";
        DateTime expectedStart = DateTime.ParseExact("10-03-2026", "dd-MM-yyyy", CultureInfo.InvariantCulture);

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.StartDateTime.Should().Be(expectedStart);
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidStartDate_ReturnsNull()
    {
        string url = _baseAddress + "?startDate=3/10/2026";

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.StartDateTime.Should().BeNull();
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidStartTime_ParsesValue()
    {
        string url = _baseAddress + "?startDate=10-03-2026&startTime=10-11";
        DateTime expectedStart = DateTime.ParseExact("10-03-2026 10:11", "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.StartDateTime.Should().Be(expectedStart);
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidStartTime_ReturnsDefault()
    {
        string url = _baseAddress + "?startDate=10-03-2026&startTime=00-99";
        DateTime expectedStart = DateTime.ParseExact("10-03-2026", "dd-MM-yyyy", CultureInfo.InvariantCulture);

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.StartDateTime.Should().Be(expectedStart);
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidEndDate_ParsesValue()
    {
        string url = _baseAddress + "?endDate=11-03-2027";
        DateTime expectedEnd = DateTime.ParseExact("11-03-2027", "dd-MM-yyyy", CultureInfo.InvariantCulture);

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.EndDateTime.Should().Be(expectedEnd);
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidEndDate_ReturnsNull()
    {
        string url = _baseAddress + "?endDate=3/11/2027";

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.EndDateTime.Should().BeNull();
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidEndTime_ParsesValue()
    {
        string url = _baseAddress + "?endDate=11-03-2027&endTime=11-12";
        DateTime expectedEnd = DateTime.ParseExact("11-03-2027 11:12", "dd-MM-yyyy HH:mm", CultureInfo.InvariantCulture);

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.EndDateTime.Should().Be(expectedEnd);
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidEndTime_ReturnsDefault()
    {
        string url = _baseAddress + "?endDate=11-03-2027&endTime=00-99";
        DateTime expectedEnd = DateTime.ParseExact("11-03-2027", "dd-MM-yyyy", CultureInfo.InvariantCulture);

        var state = _historyStateUrlService.GetStateFromUrl(url);

        state.EndDateTime.Should().Be(expectedEnd);
    }

    [TestMethod]
    public void GetStateFromUrl_WithValidColumnFilter_ParsesValue()
    {
        string url = _baseAddress + "?column=Channel-contains-SomeChannel";

        HistoryState state = _historyStateUrlService.GetStateFromUrl(url);

        state.ColumnFilters.Should().NotBeEmpty();
        state.ColumnFilters[0].ColumnName.Should().Be(nameof(PacketDto.Channel));
        state.ColumnFilters[0].Operator.Should().Be(FilterOperator.String.Contains);
        state.ColumnFilters[0].Value.Should().Be("SomeChannel");
    }

    [TestMethod]
    public void GetStateFromUrl_WithMultipleColumnFilters_ParsesValues()
    {
        string url = _baseAddress +
            "?column=channel-contains-SomeChannel" +
            "&column=STATUS-is-Processed" +
            "&column=ParentId-%3E%3D-20";

        HistoryState state = _historyStateUrlService.GetStateFromUrl(url);

        state.ColumnFilters.Should().HaveCount(3);

        state.ColumnFilters[0].ColumnName.Should().Be(nameof(PacketDto.Channel));
        state.ColumnFilters[0].Operator.Should().Be(FilterOperator.String.Contains);
        state.ColumnFilters[0].Value.Should().Be("SomeChannel");

        state.ColumnFilters[1].ColumnName.Should().Be(nameof(PacketDto.Status));
        state.ColumnFilters[1].Operator.Should().Be(FilterOperator.Enum.Is);
        state.ColumnFilters[1].Value.Should().Be(PacketStatus.Processed.ToString());

        state.ColumnFilters[2].ColumnName.Should().Be(nameof(PacketDto.ParentId));
        state.ColumnFilters[2].Operator.Should().Be(FilterOperator.Number.GreaterThanOrEqual);
        state.ColumnFilters[2].Value.Should().Be("20");
    }

    [TestMethod]
    public void GetStateFromUrl_WithInvalidColumnFilters_SkipsInvalid()
    {
        string url = _baseAddress +
                    "?column= " +
                    "&column=invalid-format" +
                    "&column=InvalidColumn-equals-data" +
                    "&column=Id-contains-10" +
                    "&column=Data-contains-valid";

        HistoryState state = _historyStateUrlService.GetStateFromUrl(url);

        state.ColumnFilters.Should().HaveCount(1);
        state.ColumnFilters[0].ColumnName.Should().Be(nameof(PacketDto.Data));
        state.ColumnFilters[0].Operator.Should().Be(FilterOperator.String.Contains);
        state.ColumnFilters[0].Value.Should().Be("valid");
    }

    [TestMethod]
    public void GetStateFromUrl_WithAllParameters_ParsesAll()
    {
        string url = _baseAddress +
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
       
        HistoryState state = _historyStateUrlService.GetStateFromUrl(url);

        DateTime expectedStart = DateTime.ParseExact("10-03-2026 10-11", "dd-MM-yyyy HH-mm", CultureInfo.InvariantCulture);
        DateTime expectedEnd = DateTime.ParseExact("11-03-2026 11-12", "dd-MM-yyyy HH-mm", CultureInfo.InvariantCulture);

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

    [TestMethod]
    public void GetStateFromUrl_AllColumnOperators_ParsesAll()
    {
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
        string url = _baseAddress + '?' + queryString;

        HistoryState state = _historyStateUrlService.GetStateFromUrl(url);

        state.ColumnFilters.Should().HaveCount(expectedCount);
    }

    [TestMethod]
    public void AppendStateQuery_WithConnector_AppendsExpectedQuery()
    {
        var connector = new ConnectorIdentifier("Instance", "MyConnector");
        var state = new HistoryState
        {
            Connector = connector
        };

        string resultUrl = HistoryStateUrlService.AppendStateQuery(_baseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        queryParams.Keys.Should().Contain("Connector");
        queryParams["Connector"].ToString().Should().Be("MyConnector");
    }

    [TestMethod]
    public void AppendStateQuery_WithView_AppendsExpectedQuery()
    {
        var state = new HistoryState
        {
            View = 3,
        };

        string resultUrl = HistoryStateUrlService.AppendStateQuery(_baseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        queryParams.Keys.Should().Contain("View");
        queryParams["View"].ToString().Should().Be("3");
    }

    [TestMethod]
    public void AppendStateQuery_WithLiveMod_AppendsExpectedQuery()
    {
        var state = new HistoryState
        {
            LiveMode = false
        };

        string resultUrl = HistoryStateUrlService.AppendStateQuery(_baseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        queryParams.Keys.Should().Contain("LiveMode");
        queryParams["LiveMode"].ToString().Should().Be("False");
    }

    [TestMethod]
    public void AppendStateQuery_WithStartDateTime_AppendsExpectedQuery()
    {
        var startDateTime = DateTime.Now;
        var expectedStartDate = startDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var expectedStartTime = startDateTime.ToString("HH-mm", CultureInfo.InvariantCulture);

        var state = new HistoryState
        {
            StartDateTime = startDateTime,
        };

        string resultUrl = HistoryStateUrlService.AppendStateQuery(_baseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        queryParams.Keys.Should().Contain(["StartDate", "StartTime"]);
        queryParams["StartDate"].ToString().Should().Be(expectedStartDate);
        queryParams["StartTime"].ToString().Should().Be(expectedStartTime);
    }

    [TestMethod]
    public void AppendStateQuery_WithEndDateTime_AppendsExpectedQuery()
    {
        var endDateTime = DateTime.Now;
        var expectedEndDate = endDateTime.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);
        var expectedEndTime = endDateTime.ToString("HH-mm", CultureInfo.InvariantCulture);

        var state = new HistoryState
        {
            EndDateTime = endDateTime,
        };

        string resultUrl = HistoryStateUrlService.AppendStateQuery(_baseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        queryParams.Keys.Should().Contain(["EndDate", "EndTime"]);
        queryParams["EndDate"].ToString().Should().Be(expectedEndDate);
        queryParams["EndTime"].ToString().Should().Be(expectedEndTime);
    }

    [TestMethod]
    public void AppendStateQuery_WithColumnFilters_AppendsExpectedQuery()
    {
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

        string resultUrl = HistoryStateUrlService.AppendStateQuery(_baseAddress, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        queryParams.Keys.Should().Contain("Column");
        var columnValues = queryParams["Column"];
        columnValues.Should().Contain($"{state.ColumnFilters[0].ColumnName}-{state.ColumnFilters[0].Operator}-{state.ColumnFilters[0].Value}");
        columnValues.Should().Contain($"{state.ColumnFilters[1].ColumnName}-{state.ColumnFilters[1].Operator}-{state.ColumnFilters[1].Value}");
        columnValues.Should().Contain($"{state.ColumnFilters[2].ColumnName}-{state.ColumnFilters[2].Operator}-{state.ColumnFilters[2].Value}");
    }

    [TestMethod]
    public void AppendStateQuery_WhenUrlHasQuery_ReplacesQuery()
    {
        var state = new HistoryState
        {
            Connector = new ConnectorIdentifier("Instance", "MyConnector")
        };

        string urlWithQuery = _baseAddress + "?existingParam=existingValue";
        string resultUrl = HistoryStateUrlService.AppendStateQuery(urlWithQuery, state);
        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

        queryParams.Keys.Should().NotContain("existingParam");
        queryParams.Keys.Should().Contain("Connector");
        queryParams["Connector"].ToString().Should().Be("MyConnector");
    }

    [TestMethod]
    public void AppendStateQuery_WithAllParameters_AppendsExpectedQuery()
    {
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
                    Value = PacketStatus.Processed.ToString()
                }
            ]
        };

        string resultUrl = HistoryStateUrlService.AppendStateQuery(_baseAddress, state);

        var uri = new Uri(resultUrl);
        var queryParams = QueryHelpers.ParseQuery(uri.Query);

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
