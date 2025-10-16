using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;
using eHub.UI.Models;

namespace eHub.UI.State;
internal class JsonViewerScopedContext : IConnectorScopedContext
{
    private readonly ConnectorIdentifier _connectorIdentifier;
    private readonly UIViewConfig _uiViewConfig;
    private readonly ConnectorPacketsExportDto _exportData;
    public event OnConnectorPacketsChangedDelegate? OnConnectorPacketsChanged;

    public bool IsFilterRunning => false;
    public FilterOptions FilterOptions { get; set; }
    public IReadOnlyCollection<PacketDto> Packets { get; set; }
    public IReadOnlyDictionary<PacketGroupIdentifier, HashSet<PacketDto>> GroupedPackets { get; set; }

    public JsonViewerScopedContext(
        ConnectorIdentifier connectorIdentifier,
        ConnectorPacketsExportDto exportData,
        DateTime startDateTime,
        DateTime endDateTime)
    {
        _connectorIdentifier = connectorIdentifier;
        _exportData = exportData;
        _uiViewConfig = CreateJsonViewerView();

        FilterOptions = new()
        {
            StartDateTime = startDateTime,
            EndDateTime = endDateTime
        };

        ApplyFilter();
    }

    [MemberNotNull(nameof(Packets), nameof(GroupedPackets))]
    public void ApplyFilter(DateTime? startDateTime = null, DateTime? endDateTime = null, IEnumerable<PacketColumnFilter>? columnFilters = null)
    {
        FilterOptions.StartDateTime = startDateTime ?? FilterOptions.StartDateTime;
        FilterOptions.EndDateTime = endDateTime ?? FilterOptions.EndDateTime;

        Packets = _exportData.Packets.Where(p => p.DateCreated >= FilterOptions.StartDateTime && p.DateCreated < FilterOptions.EndDateTime).ToArray();
        GroupedPackets = Packets
            .GroupBy(p => new PacketGroupIdentifier(p.ConnectorName, p.Channel))
            .ToDictionary(group => group.Key, group => group.ToHashSet());

        OnConnectorPacketsChanged?.Invoke(_connectorIdentifier, ConnectorPacketsChangedDto.Any);
    }

    private UIViewConfig CreateJsonViewerView()
    {
        var channels = _exportData.Packets.Select(p => p.Channel).ToHashSet();

        return new UIViewConfig()
        {
            Views = new Dictionary<string, ViewConfig>()
            {
                {
                    "All",
                    new ViewConfig()
                    {
                        Name = "All",
                        Channels = channels.Select(c => new ChannelConfig() {
                            Name = c,
                            Channel = c,
                            Position = new(1, 1)
                        }).ToList()
                    }
                }
            }
        };
    }

    public IReadOnlyDictionary<string, Type> GetCustomFilters() => FrozenDictionary<string, Type>.Empty;
    public UIViewConfig GetUIViewConfig() => _uiViewConfig;
    public Task RunAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StartFilterRunner() => throw new NotImplementedException("Can not start filter runner while in JSON Viewer.");
    public Task StopFilterRunner() => throw new NotImplementedException("Can not stop filter runner while in JSON Viewer.");
    public Task StopPacketsAsync(HashSet<PacketDto> packets) => throw new NotImplementedException("Can not ptop packet processing while in JSON Viewer.");
    public Task DeletePacketsAsync(HashSet<PacketDto> packets) => throw new NotImplementedException("Can not delete packets runner while in JSON Viewer.");
    public Task ResendPacketsAsync(HashSet<PacketDto> packets) => throw new NotImplementedException("Can not resend packets while in JSON Viewer.");
    public Task<bool> ImportPacketsAsync(ConnectorPacketsExportDto importData, bool forceImport) => throw new NotImplementedException("Can not import packets while in JSON Viewer.");
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
