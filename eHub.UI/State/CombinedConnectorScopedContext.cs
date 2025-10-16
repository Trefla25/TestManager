using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;
using eHub.UI.Models;
using eHub.UI.Services;
using eHub.UI.Util;
using System.Collections.Immutable;
using Microsoft.Extensions.Logging;

namespace eHub.UI.State;
internal class CombinedConnectorScopedContext(
    ConnectorIdentifier connectorIdentifier,
    IConnectorContext connectorContext,
    ConnectorUiData connectorUiData,
    IConnectorRegistry connectorRegistry,
    IConnectorScopedContextProvider connectorScopedContextProvider,
    ILogger<CombinedConnectorScopedContext> logger)
    : CombinedContextManager<IConnectorScopedContext>(
            connectorIdentifier,
            connectorUiData,
            connectorRegistry,
            logger), IConnectorScopedContext
{
    private readonly IConnectorScopedContextProvider _scopedContextProvider = connectorScopedContextProvider;
    private readonly IConnectorContext _connectorContext = connectorContext;

    private readonly Dictionary<ConnectorPacketIdentifier, PacketDto> _packets = [];
    private readonly Dictionary<PacketGroupIdentifier, HashSet<PacketDto>> _packetGroups = [];
    private bool _isFilterRunning;

    public event OnConnectorPacketsChangedDelegate? OnConnectorPacketsChanged;

    public bool IsFilterRunning => _isFilterRunning;
    public FilterOptions FilterOptions { get; } = new()
    {
        StartDateTime = DateTime.Now - (connectorUiData.UIViewConfig.Filter?.StartDateTimeOffset ?? TimeSpan.FromHours(1)),
        EndDateTime = DateTime.Now + (connectorUiData.UIViewConfig.Filter?.EndDateTimeOffset ?? DateTime.Now.AddYears(1) - DateTime.Now)
    };

    public IReadOnlyCollection<PacketDto> Packets => _packets.Values;
    public IReadOnlyDictionary<PacketGroupIdentifier, HashSet<PacketDto>> GroupedPackets => _packetGroups;

    private readonly Lock _sync = new();

    protected override IConnectorScopedContext GetContext(ConnectorIdentifier identifier)
        => _scopedContextProvider.GetConnectorScopedContext(identifier);

    protected override void OnInnerConnectorPacketsChanged(ConnectorIdentifier connectorIdentifier, ConnectorPacketsChangedDto changed)
    {
        bool hasChanged;

        lock (_sync)
        {
            hasChanged = PacketHelper.AggregatePacketCollections(
            incomingPackets: Connectors[connectorIdentifier.ConnectorKey].Packets,
            packetDictionary: _packets,
            groupDictionary: _packetGroups,
            keySelector: p => new ConnectorPacketIdentifier(connectorIdentifier, p.Id),
            groupSelector: p => new PacketGroupIdentifier(p.ConnectorName, p.Channel),
            prunePackets: true,
            keysToKeepSelector: (x) => x.ConnectorIdentifier != connectorIdentifier);
        }

        if(_isFilterRunning && Connectors.Values.All(c => !c.IsFilterRunning))
        {
            _isFilterRunning = false;
            hasChanged = true;
        }

        if (hasChanged)
        {
            OnConnectorPacketsChanged?.Invoke(ConnectorIdentifier, ConnectorPacketsChangedDto.Any);
        }
    }

    public void ApplyFilter(DateTime? startDateTime = null, DateTime? endDateTime = null, IEnumerable<PacketColumnFilter>? columnFilters = null)
    {
        foreach (var scopedContext in Connectors.Values)
        {
            scopedContext.ApplyFilter(startDateTime, endDateTime, columnFilters);
        }

        FilterOptions.StartDateTime = startDateTime ?? FilterOptions.StartDateTime;
        FilterOptions.EndDateTime = endDateTime ?? FilterOptions.EndDateTime;
        FilterOptions.ColumnFilters = columnFilters?.ToImmutableArray() ?? FilterOptions.ColumnFilters;
    }

    public async Task StartFilterRunner()
    {
        _isFilterRunning = true;
        foreach (var scopedContext in Connectors.Values)
        {
            await scopedContext.StartFilterRunner();
        }
    }

    public async Task StopFilterRunner()
    {
        _isFilterRunning = false;
        foreach (var scopedContext in Connectors.Values)
        {
            await scopedContext.StopFilterRunner();
        }
    }

    public Task RunAsync(CancellationToken cancellationToken) => _connectorContext.RunAsync(cancellationToken);
    public IReadOnlyDictionary<string, Type> GetCustomFilters() => _connectorContext.GetCustomFilters();
    public UIViewConfig GetUIViewConfig() => _connectorContext.GetUIViewConfig();
    public Task DeletePacketsAsync(HashSet<PacketDto> packets) => _connectorContext.DeletePacketsAsync(packets);
    public Task<bool> ImportPacketsAsync(ConnectorPacketsExportDto importData, bool forceImport) => _connectorContext.ImportPacketsAsync(importData, forceImport);
    public Task ResendPacketsAsync(HashSet<PacketDto> packets) => _connectorContext.ResendPacketsAsync(packets);
    public Task StopPacketsAsync(HashSet<PacketDto> packets) => _connectorContext.StopPacketsAsync(packets);

    private readonly record struct ConnectorPacketIdentifier(ConnectorIdentifier ConnectorIdentifier, long PacketId);
}
