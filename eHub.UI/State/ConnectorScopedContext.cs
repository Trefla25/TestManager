using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eMessenger;
using eController.Util;
using Microsoft.Extensions.Logging;
using eHub.PlugIn.UI;
using System.Collections.Immutable;
using System.Threading.Channels;
using eHub.UI.Models;
using eHub.UI.Util;

namespace eHub.UI.State;
public class ConnectorScopedContext : IConnectorScopedContext
{
    private readonly ConnectorIdentifier _connectorIdentifier;
    private readonly IConnectorContext _connectorContext;
    private readonly ConnectorUiData _connectorUiData;
    private readonly IMessenger _messenger;
    private readonly ILogger<ConnectorScopedContext> _logger;

    private readonly Dictionary<long, PacketDto> _packets = [];
    private readonly Dictionary<PacketGroupIdentifier, HashSet<PacketDto>> _packetGroups = [];

    private readonly CancellationTokenSource _refreshStopSource = new();
    private readonly SemaphoreSlim _reloadSemaphore = new(1, 1);
    private readonly string _filterRunnerIdentifier = Guid.NewGuid().ToString();
    private readonly Channel<ConnectorPacketsFilterDto> _refreshRequests;
    private IRegistrationToken _filterRegToken = NullRegistrationToken.Instance;
    private bool _isFilterRunning;
    private bool _isRefreshEnabled;

    public event OnConnectorPacketsChangedDelegate? OnConnectorPacketsChanged;

    public bool IsFilterRunning => _isFilterRunning;
    public FilterOptions FilterOptions { get; init; }
    public IReadOnlyCollection<PacketDto> Packets => _packets.Values;
    public IReadOnlyDictionary<PacketGroupIdentifier, HashSet<PacketDto>> GroupedPackets => _packetGroups;

    public ConnectorScopedContext(
        ConnectorIdentifier connectorIdentifier,
        IConnectorContext connectorContext,
        ConnectorUiData connectorUiData,
        IMessenger messenger,
        ILogger<ConnectorScopedContext> logger)
    {
        _connectorContext = connectorContext;
        _connectorIdentifier = connectorIdentifier;
        _connectorUiData = connectorUiData;

        _messenger = messenger;
        _logger = logger;

        _refreshRequests = Channel.CreateUnbounded<ConnectorPacketsFilterDto>();

        FilterOptions = new()
        {
            StartDateTime = DateTime.Now - (_connectorUiData.UIViewConfig.Filter?.StartDateTimeOffset ?? TimeSpan.FromHours(1)),
            EndDateTime = DateTime.Now + (_connectorUiData.UIViewConfig.Filter?.EndDateTimeOffset ?? DateTime.Now.AddYears(1) - DateTime.Now)
        };

        connectorContext.OnConnectorPacketsChanged += ConnectorPacketsChangedHandler;

        _isRefreshEnabled = true;
        _ = TaskUtil.RunNonBlocking(async () => await PacketsAutoRefreshAsync(_refreshStopSource.Token));
    }

    public void ApplyFilter(DateTime? startDateTime = null, DateTime? endDateTime = null, IEnumerable<PacketColumnFilter>? columnFilters = null)
    {
        FilterOptions.StartDateTime = startDateTime ?? FilterOptions.StartDateTime;
        FilterOptions.EndDateTime = endDateTime ?? FilterOptions.EndDateTime;
        FilterOptions.ColumnFilters = columnFilters?.ToImmutableArray() ?? FilterOptions.ColumnFilters;

        _refreshRequests.Writer.TryWrite(ConnectorPacketsFilterDto.All);
    }

    public async Task ReloadPackets(CancellationToken cancellationToken = default)
    {
        // Only allow one reload at a time
        await _reloadSemaphore.WaitAsync(cancellationToken);

        // Accumulate multiple requests to avoid flooding
        await Task.Delay(50, cancellationToken);

        try
        {
            // Start with "no hint" and accumulate any incoming refresh requests.
            var refreshHint = ConnectorPacketsFilterDto.None;
            while (_refreshRequests.Reader.TryRead(out var request))
            {
                refreshHint = refreshHint.Union(request);
            }

            // If no filter hint was read, default to All
            if (refreshHint == ConnectorPacketsFilterDto.None)
            {
                refreshHint = ConnectorPacketsFilterDto.All;
            }

            var prunePackets = refreshHint == ConnectorPacketsFilterDto.All;

            var uiFilter = new ConnectorPacketsFilterDto(
                DateTimeStart: FilterOptions.StartDateTime,
                DateTimeEnd: FilterOptions.EndDateTime);

            // Only keep the packets that overlap with the UI's date range
            refreshHint = refreshHint.Intersect(uiFilter);
            var filters = FilterOptions.ColumnFilters.Select(f => f.ToColumnFilterDto()).ToImmutableArray();
            var requestDto = refreshHint.ToRequestWith(
                limit: _connectorUiData.UIViewConfig.ViewPacketCount,
                columnFilters: filters);

            if (requestDto.IsEmpty)
            {
                return;
            }

            var response = await _messenger.AskAsync<PacketRequestDto, PacketWrapperDto>(
                ConnectorContract.UIPacketsGetTopic(_connectorIdentifier), requestDto)
                .FirstOrDefaultResponse();

            if (response is null)
            {
                return;
            }

            var hasChanged = PacketHelper.AggregatePacketCollections(
                incomingPackets: response.Packets,
                packetDictionary: _packets,
                groupDictionary: _packetGroups,
                keySelector: p => p.Id,
                groupSelector: p => new PacketGroupIdentifier(p.ConnectorName, p.Channel),
                prunePackets: prunePackets);

            if (hasChanged)
            {
                OnConnectorPacketsChanged?.Invoke(_connectorIdentifier, ConnectorPacketsChangedDto.Any);
            }
        }
        finally
        {
            _reloadSemaphore.Release();
        }
    }

    public async Task StartFilterRunner()
    {
        await _filterRegToken.DisposeAsync();
        _filterRegToken = NullRegistrationToken.Instance;
        _isRefreshEnabled = false;

        _filterRegToken += await _messenger.ListenAsync<ConnectorIdentifier>(
            ConnectorContract.FilterRunnerStoppedNotification(_filterRunnerIdentifier),
            FilterRunnerStopped);

        _filterRegToken += await _messenger.AnswerAsync<PacketWrapperDto, bool>(
            ConnectorContract.PushFilteredPacketsTopic(_filterRunnerIdentifier),
            FilterRunnerPacketReceived);

        var columnFilters = FilterOptions.ColumnFilters.Select(x => x.ToColumnFilterDto()).ToImmutableArray();
        var topPacketsCount = _connectorUiData.UIViewConfig.ViewPacketCount;

        var requestFilterDto = new PacketRequestDto(
            DateTimeStart: FilterOptions.StartDateTime,
            DateTimeEnd: FilterOptions.EndDateTime,
            Limit: topPacketsCount,
            ColumnFilters: columnFilters);

        await _messenger.SendAsync(
            ConnectorContract.StartFilterRunnerTopic(_connectorIdentifier),
            new FilterRunnerRequest(requestFilterDto, _filterRunnerIdentifier));

        _isFilterRunning = true;
    }

    public async Task StopFilterRunner()
    {
        _isRefreshEnabled = true;
        await _messenger.SendAsync(ConnectorContract.StopFilterRunnerTopic(_connectorIdentifier), _filterRunnerIdentifier);
    }

    private void FilterRunnerStopped(ConnectorIdentifier connectorIdentifier)
    {
        _isFilterRunning = false;

        OnConnectorPacketsChanged?.Invoke(_connectorIdentifier, ConnectorPacketsChangedDto.Any);
    }

    private bool FilterRunnerPacketReceived(PacketWrapperDto packetWrapperDto)
    {
        try
        {
            PacketHelper.AggregatePacketCollections(
                incomingPackets: packetWrapperDto.Packets,
                packetDictionary: _packets,
                groupDictionary: _packetGroups,
                keySelector: p => p.Id,
                groupSelector: p => new PacketGroupIdentifier(p.ConnectorName, p.Channel),
                prunePackets: false);

            OnConnectorPacketsChanged?.Invoke(_connectorIdentifier, ConnectorPacketsChangedDto.Any);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while adding packets from FilterRunner.");
            return false;
        }
    }

    private void ConnectorPacketsChangedHandler(ConnectorIdentifier connectorIdentifier, ConnectorPacketsChangedDto changed)
    {
        if (_isRefreshEnabled)
        {
            _refreshRequests.Writer.TryWrite(changed.FilterHint);
        }
    }

    private async Task PacketsAutoRefreshAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (_isRefreshEnabled)
                {
                    await ReloadPackets(cancellationToken);
                }

                await _refreshRequests.Reader.WaitToReadAsync(cancellationToken);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Encountered error while loading packets.");
            }
        }
    }

    public Task RunAsync(CancellationToken cancellationToken) => _connectorContext.RunAsync(cancellationToken);
    public IReadOnlyDictionary<string, Type> GetCustomFilters() => _connectorContext.GetCustomFilters();
    public UIViewConfig GetUIViewConfig() => _connectorContext.GetUIViewConfig();
    public Task ResendPacketsAsync(HashSet<PacketDto> selectedPackets) => _connectorContext.ResendPacketsAsync(selectedPackets);
    public Task StopPacketsAsync(HashSet<PacketDto> selectedPackets) => _connectorContext.StopPacketsAsync(selectedPackets);
    public Task DeletePacketsAsync(HashSet<PacketDto> selectedPackets) => _connectorContext.DeletePacketsAsync(selectedPackets);
    public Task<bool> ImportPacketsAsync(ConnectorPacketsExportDto importData, bool forceImport) => _connectorContext.ImportPacketsAsync(importData, forceImport);

    public async ValueTask DisposeAsync()
    {
        await _filterRegToken.DisposeAsync();
        _connectorContext.OnConnectorPacketsChanged -= ConnectorPacketsChangedHandler;

        if (_refreshStopSource != null)
        {
            await _refreshStopSource.CancelAsync();
            _refreshStopSource.Dispose();
        }
    }
}

