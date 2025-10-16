using System.Collections.Frozen;
using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.UIConfig;

namespace eHub.UI.Models;

public class HistoryState
{
    public ConnectorIdentifier? Connector { get; set; }
    public int View { get; set; }
    public bool LiveMode { get; set; } = true;

    public DateTime? StartDateTime { get; set; }
    public DateTime? EndDateTime { get; set; }
    public ImmutableArray<PacketColumnFilter> ColumnFilters { get; set; } = [];
}
