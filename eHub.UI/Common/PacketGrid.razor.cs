using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eHub.UI.Localization;
using eHub.UI.Util;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;

namespace eHub.UI.Common;

public partial class PacketGrid : FrameworkComponent
{
    [Inject] public required IJSRuntime JSRuntime { get; init; }
    [Inject] public required ILogger<PacketGrid> Logger { get; init; }
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }
    [Inject] public required IDialogService DialogService { get; init; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; } = default!;

    [Parameter] public required IReadOnlyList<PacketDto> Packets { get; set; }
    [Parameter] public required IReadOnlySet<PacketDto> SelectedPackets { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public bool Loading { get; set; }
    [Parameter] public bool ShowChannelColumn { get; set; }
    [Parameter] public bool GroupPackets { get; set; }
    [Parameter] public bool LockScroll { get; set; }
    [Parameter] public bool IsExportViewerMode { get; set; }

    [Parameter] public Dictionary<string, ColumnConfig>? Columns { get; set; }
    [Parameter] public DisplayFormat? DataDisplayFormat { get; set; }
    [Parameter] public int? DataRowCount { get; set; }
    [Parameter] public int? ViewPacketCount { get; set; }
    [Parameter] public bool LiveMode { get; set; }
    [Parameter] public bool FilterRunning { get; set; }
    [Parameter] public string? Height { get; set; }
    [Parameter] public long? ParentId { get; set; }

    [Parameter] public EventHandler<GridScrollEventArgs>? OnGridScroll { get; set; }
    [Parameter] public Action<PacketDto>? OnPacketResendClick { get; set; }
    [Parameter] public Action<PacketDto>? OnPacketRowClick { get; set; }
    [Parameter] public Action? OnFilterRunnerStopped { get; set; }
    [Parameter] public Action? OnFilterRunnerStarted { get; set; }

    private MudDataGrid<PacketGridRow>? _grid;
    private ElementReference _packetGridRef;
    private DotNetObjectReference<PacketGrid> _dotNetReference = default!;

    private readonly SortedDictionary<PacketGridRowId, PacketGridRow> _gridPackets = [];

    private readonly DataDisplayConfig _dataDisplay = new();
    private string _searchString = "";
    private bool _isAdministrator;

    private HashSet<PacketStatus> _filterStatuses = [];
    private HashSet<PacketStatus> _selectedStatuses = [];

    private FilterDefinition<PacketGridRow> _statusFilterDefinition = default!;
    private bool _statusFilterSelectAll = true;
    private bool _showLabel = true;
    private int _rowsPerPage;
    private int RowsPerPage {
        get => _rowsPerPage;
        set
        {
            if (value < 1)
            {
                return;
            }

            if (_rowsPerPage != value)
            {
                _showLabel = false;
                _rowsPerPage = value;
                OnRowsPerPageChanged();
            }
        }
    }
    private IEnumerable<int> _rowsPerPageList = [10, 25, 50, 100, 250, 500, 1000, 5000, 10000];


    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _isAdministrator = authState.User?.Claims.Any(c => c.Value == "Administrator") ?? false;

        RowsPerPage = ViewPacketCount ?? 100;
        if (!_rowsPerPageList.Contains(RowsPerPage))
        {
            _rowsPerPageList = _rowsPerPageList.Union([RowsPerPage]).OrderBy(x => x);
        }

        _selectedStatuses = [.. PacketHelper.Statuses];
        _filterStatuses = [.. PacketHelper.Statuses];
        _statusFilterDefinition = new() { FilterFunction = StatusFilterCheck };
    }

    protected override void OnParametersSet()
    {
        FillGridPackets();

        _dataDisplay.Format = DataDisplayFormat ?? DefaultGridConfig.DisplayFormat;

        if (DataDisplayFormat?.IsScroll() == true)
        {
            _dataDisplay.RowCount = DataRowCount ?? DefaultGridConfig.DataRowCount;
        }

        Columns ??= DefaultGridConfig.Columns;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _dotNetReference = DotNetObjectReference.Create(this);

            await JSRuntime.InvokeVoidAsync("packetGrid.attachGridEvents", _dotNetReference, _packetGridRef);
        }
    }

    public async Task ResetGridAsync()
    {
        if (_grid is null)
        {
            return;
        }

        await _grid.ClearFiltersAsync();
        _filterStatuses = [.. PacketHelper.Statuses];
        _selectedStatuses = [.. PacketHelper.Statuses];
        _statusFilterSelectAll = true;

        foreach (var sort in _grid.SortDefinitions)
        {
            await _grid.RemoveSortAsync(sort.Key);
        }
    }

    private void FillGridPackets()
    {
        if (GroupPackets)
        {
            // Mark all packets as not to keep
            foreach (var packetRow in _gridPackets.Values)
            {
                packetRow.Keep = false;
            }

            foreach (var packet in Packets)
            {
                var identifier = GetIdentifier(packet);
                if (_gridPackets.TryGetValue(identifier, out var packetRow))
                {
                    packetRow.Packet = packet;
                    packetRow.Keep = true;
                }
                else
                {
                    _gridPackets.Add(identifier, new PacketGridRow() { Packet = packet, ShowChildContent = false, Keep = true });
                }
            }

            // store all the searched parents
            Dictionary<PacketGridRowId, bool> searchedParents = _gridPackets.Values
                .Select(p => (p.Packet.ConnectorName, p.Packet.ParentId))
                .Where(x => x.ParentId != null)
                .Distinct()
                .ToDictionary(x =>
                    new PacketGridRowId(x.ConnectorName, x.ParentId!.Value),
                    _ => false);

            // set to true all found searched packets
            Packets.Where(p => searchedParents.ContainsKey(GetIdentifier(p))).ForEach(p => searchedParents[GetIdentifier(p)] = true);

            // Unmark all child packets with existing parents (the rest are considered normal packets)
            foreach (var gridRow in _gridPackets.Values)
            {
                if (gridRow.Packet.ParentId is null)
                {
                    continue;
                }

                var parentIdentifier = new PacketGridRowId(gridRow.Packet.ConnectorName, gridRow.Packet.ParentId.Value);
                if (searchedParents.ContainsKey(parentIdentifier))
                {
                    gridRow.Keep = false;
                }
            }

            // Remove all packets that are not marked as keep
            var packetsToRemove = _gridPackets.Where(pair => !pair.Value.Keep).Select(pair => pair.Key).ToArray();
            packetsToRemove.ForEach(p => _gridPackets.Remove(p));
        }
        else
        {
            // Mark all packets as not to keep and hide child content
            foreach (var packetRow in _gridPackets.Values)
            {
                packetRow.Keep = false;
                packetRow.ShowChildContent = false;
            }

            foreach (var packet in Packets)
            {
                var identifier = new PacketGridRowId(packet.ConnectorName, packet.Id);
                if (_gridPackets.TryGetValue(identifier, out var packetRow))
                {
                    packetRow.Packet = packet;
                    packetRow.Keep = true;
                }
                else
                {
                    _gridPackets.Add(identifier, new PacketGridRow() { Packet = packet, ShowChildContent = false, Keep = true });
                }
            }

            var packetsToRemove = _gridPackets.Where(pair => !pair.Value.Keep).Select(pair => pair.Key).ToArray();
            packetsToRemove.ForEach(p => _gridPackets.Remove(p));
        }
    }

    private bool QuickFilter(PacketGridRow packetRow) =>
        string.IsNullOrWhiteSpace(_searchString)
        || packetRow.Packet.PreviewData?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) == true
        || packetRow.Packet.Data?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) == true
        || packetRow.Packet.Channel.Contains(_searchString, StringComparison.OrdinalIgnoreCase)
        || $"{packetRow.Packet.Id}".Contains(_searchString)
        || packetRow.Packet.DynamicField?.Contains(_searchString, StringComparison.OrdinalIgnoreCase) == true;

    private string GetPacketRowStyle(PacketGridRow packetRow, int rowIndex)
    {
        var style = "height: 55px;";
        if (SelectedPackets.Any(p => p.Id == packetRow.Packet.Id) == true)
        {
            style += "background-color:silver;";
        }

        return style;
    }

    private string GetPacketDataStyle()
    {
        var style = string.Empty;

        style += "vertical-align: middle;";

        var displayFormat = (DisplayFormat)_dataDisplay.Format!;

        style += displayFormat == DisplayFormat.SingleLine
            ? "text-wrap: nowrap;"
            : "text-wrap: pretty;";


        style += displayFormat.IsRawText() == true ? "margin: 16px;" : "";

        if (displayFormat.IsScroll())
        {
            var rowCount = (int)_dataDisplay.RowCount!;
            var rowHeight = displayFormat == DisplayFormat.MultiLineScroll ? "20px" : "40px";

            style += $"max-height: calc({rowHeight} * {rowCount});"
                + "display: inline-block;"
                + "overflow: auto;";
        }

        return style;
    }

    private ColumnConfig GetColumnConfig(string columnName)
    {
        if (Columns is { } && Columns.TryGetValue(columnName, out var columnConfig) && columnConfig is { })
        {
            return columnConfig;
        }

        return new() { Visible = false };
    }

    private async Task OpenDataDialog(PacketDto packet)
    {
        try
        {
            var dialog = await DialogService.Create<DataDialog>()
                .Title(Localizer["Data"])
                .Options(new DialogOptions() { FullWidth = true, MaxWidth = MaxWidth.Medium, CloseOnEscapeKey = true, CloseButton = true })
                .Param(x => x.Data, packet.Data)
                .Param(x => x.DataType, packet.DataType)
                .Param(x => x.CanResend, packet.CanResend)
                .Param(x => x.OnPacketResendClick, (string value) => ResendChangedPacket(packet, value))
                .ShowAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open Data Dialog.");
        }
    }

    private void ResendChangedPacket(PacketDto packet, string newValue)
    {
        var newPacket = new PacketDto()
        {
            Id = packet.Id,
            Data = newValue,
            Status = packet.Status,
            Channel = packet.Channel,
            DateCreated = packet.DateCreated,
            ParentId = packet.ParentId
        };

        OnPacketResendClick?.Invoke(newPacket);
    }

    private void RowClicked(DataGridRowClickEventArgs<PacketGridRow> args)
    {
        OnPacketRowClick?.Invoke(args.Item.Packet);
    }

    private void HandleResendClick(PacketDto packet)
    {
        OnPacketResendClick?.Invoke(packet);
    }

    private void HandleFilterRunnerStateChanged()
    {
        if (FilterRunning)
        {
            OnFilterRunnerStopped?.Invoke();
            FilterRunning = false;
        }
        else
        {
            OnFilterRunnerStarted?.Invoke();
            FilterRunning = true;
        }
    }

    private bool StatusFilterCheck(PacketGridRow packetRow)
    {
        return _filterStatuses.Contains(packetRow.Packet.Status);
    }

    private void StatusFilterSelectedChanged(bool value, PacketStatus status)
    {
        if (value)
        {
            _selectedStatuses.Add(status);
        }
        else
        {
            _selectedStatuses.Remove(status);
        }


        _statusFilterSelectAll = _selectedStatuses.Count == PacketHelper.Statuses.Count;
    }

    private async Task ClearStatusFilterAsync(FilterContext<PacketGridRow> context)
    {
        _statusFilterSelectAll = true;
        _selectedStatuses = [.. PacketHelper.Statuses];
        _filterStatuses = [.. PacketHelper.Statuses];
        await context.Actions.ClearFilterAsync(_statusFilterDefinition);
    }

    private async Task ApplyStatusFilterAsync(FilterContext<PacketGridRow> context)
    {
        _filterStatuses = [.. _selectedStatuses];
        await context.Actions.ApplyFilterAsync(_statusFilterDefinition);
    }

    private void SelectAllStatusFilters(bool value)
    {
        _statusFilterSelectAll = value;

        if (value)
        {
            _selectedStatuses = [.. PacketHelper.Statuses];
        }
        else
        {
            _selectedStatuses.Clear();
        }
    }

    public async Task ClearFiltersAsync()
    {
        await _grid.ClearFiltersAsync();
    }

    public async Task ReloadGrid(bool liveMode)
    {
        if (_grid != null)
        {
            await _grid.SetRowsPerPageAsync(liveMode ? int.MaxValue : _rowsPerPage/*ViewPacketCount ?? int.MaxValue*/);
        }
    }

    private string GetHeight() => LiveMode ? "calc(100% - 64px)" : "calc(100% - 64px - 52px)";

    public async Task ScrollToPacket(long id)
    {
        await JSRuntime.InvokeVoidAsync("packetGrid.scrollToItemWithClass", _packetGridRef, GetClassNameById(id));
    }

    [JSInvokable("HandleScroll")]
    public Task HandleScroll(string rowClass)
    {
        try
        {
            if (LockScroll)
            {
                var id = GetIdByClassName(rowClass);

                OnGridScroll?.Invoke(this, new GridScrollEventArgs() { PacketId = id });
            }

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "HandleScroll");
        }

        return Task.CompletedTask;
    }

    private static string GetClassNameById(long id)
    {
        return $"packet-row-{id}";
    }

    private static long GetIdByClassName(string className)
    {
        var classNameStart = "packet-row-";

        var startIndex = className.IndexOf(classNameStart);

        if (startIndex == -1 || !long.TryParse(className.AsSpan(startIndex + classNameStart.Length), out long id))
        {
            throw new Exception("Invalid class name format");
        }

        return id;
    }

    public static SortDirection GetSortDirectionFromString(string? direction) => direction switch
    {
        "Ascending" => SortDirection.Ascending,
        "Descending" => SortDirection.Descending,
        _ => SortDirection.None
    };

    public static string? GetStyleFromWidth(int? width) => width is null ? string.Empty : $"width: {width}px;";

    public static string? GetStatusIcon(PacketStatus status) => status switch
    {
        PacketStatus.Enqueued => Icons.Material.Rounded.HourglassTop,
        PacketStatus.InProgress => Icons.Material.Rounded.HourglassTop,
        PacketStatus.Error => Icons.Material.Rounded.Warning,
        PacketStatus.FatalError => Icons.Material.Rounded.Error,
        PacketStatus.ManualStop => Icons.Material.Filled.StopCircle,
        _ => null
    };

    public static Color GetStatusIconColor(PacketStatus status) => status switch
    {
        PacketStatus.Enqueued => Color.Info,
        PacketStatus.InProgress => Color.Info,
        PacketStatus.Error => Color.Warning,
        PacketStatus.FatalError => Color.Error,
        PacketStatus.ManualStop => Color.Warning,
        _ => Color.Default
    };

    private async Task<IEnumerable<int>> SearchRowsPerPage(string searchText)
    {
        await Task.Delay(5);

        return _rowsPerPageList;
    }

    private async Task OnRowsPerPageChanged()
    {
        if (_grid != null)
        {
            await _grid.SetRowsPerPageAsync(_rowsPerPage);
        }
    }

    private static PacketGridRowId GetIdentifier(PacketDto packet) => new(packet.ConnectorName, packet.Id);

    public class GridScrollEventArgs : EventArgs
    {
        public long PacketId { get; set; }
    }

    public record PacketGridRow
    {
        public required PacketDto Packet { get; set; }
        public bool ShowChildContent { get; set; }
        public bool Keep { get; set; }
    }

    public readonly record struct PacketGridRowId(string ConnectorName, long Id): IComparable<PacketGridRowId>
    {
        public int CompareTo(PacketGridRowId other)
        {
            int nameCmp = string.Compare(ConnectorName, other.ConnectorName, StringComparison.Ordinal);
            return nameCmp != 0 ? nameCmp : Id.CompareTo(other.Id);
        }
    }

    private static class DefaultGridConfig
    {
        public static Dictionary<string, ColumnConfig> Columns { get; set; } = new() {
            {
                nameof(PacketDto.Id), new()
                {
                    Visible = true,
                    Width = 140,
                    SortDirection = "Descending"
                }
            },
            {
                nameof(PacketDto.DateCreated), new()
                {
                    Visible = true,
                    Width = 200,
                    Format = "dd.MM.yyyy HH:mm:ss.fff"
                }
            },
            { nameof(PacketDto.DateChanged), new() { Visible = false } },
            {
                nameof(PacketDto.Channel), new()
                {
                    Visible = true,
                    Width = 185
                }
            },
            { nameof(PacketDto.ParentId), new() { Visible = false } },
            { nameof(PacketDto.RetryCount), new() { Visible = false } },
            { nameof(PacketDto.Data), new() { Visible = true } },
            { nameof(PacketDto.DynamicField), new() { Visible = false } },
            {
                nameof(PacketDto.Status), new()
                {
                    Visible = true,
                    Width = 155
                }
            }
        };
        public static DisplayFormat DisplayFormat { get; set; } = DisplayFormat.Dialog;
        public static int DataRowCount { get; set; } = 5;
        public static SubstringInfo PreviewSubstring { get; set; } = new(0, 50);
    }
}
