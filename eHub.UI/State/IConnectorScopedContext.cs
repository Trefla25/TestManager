using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.UI.Models;

namespace eHub.UI.State;
public interface IConnectorScopedContext : IConnectorContext
{
    public FilterOptions FilterOptions { get; }
    public bool IsFilterRunning { get; }

    public IReadOnlyCollection<PacketDto> Packets { get; }
    public IReadOnlyDictionary<PacketGroupIdentifier, HashSet<PacketDto>> GroupedPackets { get; }

    public void ApplyFilter(DateTime? startDateTime = null, DateTime? endDateTime = null, IEnumerable<PacketColumnFilter>? columnFilters = null);
    Task StartFilterRunner();
    Task StopFilterRunner();
}

public record FilterOptions
{
    public required DateTime StartDateTime { get; set; }
    public required DateTime EndDateTime { get; set; }
    public ImmutableArray<PacketColumnFilter> ColumnFilters { get; set; } = [];
}
