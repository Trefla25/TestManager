using eHub.Contracts.UIConfig;
using eHub.PlugIn.UI;
using eHub.UI.Localization;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;
using static eHub.UI.Common.PacketGrid;

namespace eHub.UI.Common;

public partial class ChildPacketGrid : FrameworkComponent
{
    [Inject] public required ILogger<PacketGrid> Logger { get; init; }
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }
    [Inject] public required IDialogService DialogService { get; init; }

    [Parameter] public required IReadOnlyList<PacketDto> Packets { get; set; }
    [Parameter] public required IReadOnlySet<PacketDto> SelectedPackets { get; set; }
    [Parameter] public required Func<PacketGridRow, int, string> GetPacketRowStyle { get; set; }
    [Parameter] public required Func<string> GetPacketDataStyle { get; set; }
    [Parameter] public required Func<string, ColumnConfig> GetColumnConfig { get; set; }
    [Parameter] public required Func<int?, string?> GetStyleFromWidth { get; set; }

    [Parameter] public required DataDisplayConfig DataDisplay { get; set; }
    [Parameter] public bool IsExportViewerMode { get; set; }
    [Parameter] public long? ParentId { get; set; }
    [Parameter] public bool IsAdministrator { get; set; }

    [Parameter] public Action<PacketDto>? OnPacketResendClick { get; set; }
    [Parameter] public Action<PacketDto>? OnPacketRowClick { get; set; }
    [Parameter] public Action<PacketDto>? OnOpenDataDialog { get; set; }

    private readonly SortedDictionary<long, PacketGridRow> _gridPackets = [];

    protected override void OnParametersSet()
    {
        FillGridPackets();
    }

    private void FillGridPackets()
    {
        // Mark all packets as not to keep and hide child content
        foreach (var packetRow in _gridPackets.Values)
        {
            packetRow.Keep = false;
        }

        foreach (var packet in Packets)
        {
            if (packet.ParentId != ParentId)
            {
                continue;
            }

            if (_gridPackets.TryGetValue(packet.Id, out var packetRow))
            {
                packetRow.Packet = packet;
                packetRow.Keep = true;
            }
            else
            {
                _gridPackets.Add(packet.Id, new PacketGridRow() { Packet = packet, ShowChildContent = false, Keep = true });
            }
        }

        var packetsToRemove = _gridPackets.Where(pair => !pair.Value.Keep).Select(pair => pair.Key).ToArray();
        packetsToRemove.ForEach(p => _gridPackets.Remove(p));
    }
}
