using System.Collections.Generic;
using System.Collections.Immutable;
using System.Data;
using System.Globalization;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Text.Json;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.PlugIn;
using eHub.PlugIn.UI;
using eHub.UI.Common;
using eHub.UI.Localization;
using eHub.UI.Models;
using eHub.UI.Services;
using eHub.UI.State;
using eHub.UI.Util;
using eController.WebUI.Utility.BusyService;
using eController.WebUI.Utility.DynamicComponent;
using eController.WebUI.Utility.Menu;
using eMessenger;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;
using static eHub.UI.Common.PacketFilterDialog;

namespace eHub.UI.Pages;

[MenuGroupItem(4711, "eHub", "power")]
[MenuItem("History", "history")]
[Route("/eController/IntegrationHub/UI/Pages/History")]
public partial class History : FrameworkComponent, IAsyncDisposable
{
    private static readonly ConnectorIdentifier JsonViewerIdentifier = new("<ThisWebUI>", "JsonViewer");

    [Inject] public required IConnectorRegistry ConnectorRegistry { get; init; }
    [Inject] public required IConnectorScopedContextProvider ConnectorScopedContextProvider { get; init; }
    [Inject] public required HistoryStateUrlService HistoryStateUrlService { get; init; }
    [Inject] public required IMessenger Messenger { get; init; }
    [Inject] public required IJSRuntime JSRuntime { get; init; }
    [Inject] public required IDialogService DialogService { get; init; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; } = default!;
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; } = default!;
    [Inject] public required IBusyService BusyService { get; init; }
    [Inject] public required ISnackbar Snackbar { get; init; }
    [Inject] public required NavigationManager NavigationManager { get; init; }
    [Inject] public required ILogger<History> Logger { get; init; }

    private readonly CultureInfo _currentCulture = CultureInfo.CurrentCulture;
    private readonly CancellationTokenSource _refreshStopSource = new();
    private readonly List<PacketGrid> _packetGrids = []; // TODO: Check if this has any use

    private readonly Dictionary<ConnectorIdentifier, IConnectorScopedContext> _connectorsScopedContexts = [];

    private ClaimsPrincipal? _user;
    private AuthenticationState? _authenticationState;

    private bool _loading = false;
    private bool _liveMode = true;
    private bool _groupPackets = true;
    private bool _lockScroll = false;
    private bool _isExportViewerMode = false;
    private bool _isAdministrator = false;
    private int _selectedViewIndex = 0;
    private int _windowWidth = 0;

    private HistorySidePanel? _historySidePanel;
    private MudTabs? _tabs;

    private ConnectorIdentifier? _selectedIdentifier;
    private HashSet<PacketDto> _selectedPackets = [];

    private PacketGrid PacketGridRef { set { _packetGrids.Add(value); } } // TODO: Check if this has any use

    protected override async Task OnInitializedAsync()
    {
        using var b = BusyService.QueueIndeterminateJob("Init Editor");
        ConfigureSnackbar();

        _authenticationState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _user = _authenticationState.User;
        _isAdministrator = _user?.Claims.Any(c => c.Value == "Administrator") ?? false;

        _windowWidth = await JSRuntime.InvokeAsync<int>("GetWindowWidth");

        ConnectorRegistry.ActiveConnectorsChanged += ActiveConnectorsChanged;

        await InitializeConnectors();

        var state = HistoryStateUrlService.GetStateFromUrl(NavigationManager.Uri);
        _selectedViewIndex = state.View;
        _liveMode = state.LiveMode;

        if (state.Connector is { } identifier && identifier != _selectedIdentifier)
        {
            _selectedIdentifier = identifier;
            var dateTimeStart = state.StartDateTime;
            var dateTimeEnd = state.EndDateTime;
            var columnFilters = state.ColumnFilters;

            _connectorsScopedContexts[identifier].ApplyFilter(dateTimeStart, dateTimeEnd, columnFilters);
        }

        ApplyStateToUrl();

        // Allow the page to refresh (so the new tabs are rendered) before setting the active tab
        await Task.Delay(10);
        _tabs?.ActivatePanel(_selectedViewIndex);
        await InvokeAsync(StateHasChanged);

        if (!_liveMode)
        {
            await EnterOfflineMode();
        }
    }

    private void ApplyStateToUrl()
    {
        if (_selectedIdentifier is not { } selectedIdentifier)
        {
            return;
        }

        var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];

        var state = new HistoryState()
        {
            Connector = _selectedIdentifier,
            View = _selectedViewIndex,
            LiveMode = _liveMode,
            StartDateTime = selectedConnectorScopedContext.FilterOptions.StartDateTime,
            EndDateTime = selectedConnectorScopedContext.FilterOptions.EndDateTime,
            ColumnFilters = selectedConnectorScopedContext.FilterOptions.ColumnFilters
        };

        var newUri = HistoryStateUrlService.AppendStateQuery(NavigationManager.Uri, state);
        NavigationManager.NavigateTo(newUri, replace: true);
    }

    private async Task InitializeConnectors()
    {
        using var b = BusyService.QueueIndeterminateJob("Init Connectors");

        try
        {
            var connectorsToRemove = _connectorsScopedContexts
                .Where(c => !ConnectorRegistry.ActiveConnectors.ContainsKey(c.Key))
                .ToArray();

            var connectorsToAdd = ConnectorRegistry.ActiveConnectors
                .Where(c => !_connectorsScopedContexts.ContainsKey(c.Key))
                .ToArray();

            foreach (var (connectorIdentifier, connectorScope) in connectorsToRemove)
            {
                connectorScope.OnConnectorPacketsChanged -= ConnectorPacketsChangedHandler;

                await connectorScope.DisposeAsync();
                _connectorsScopedContexts.Remove(connectorIdentifier);
            }

            foreach (var (connectorIdentifier, uiData) in connectorsToAdd)
            {
                var connectorScope = ConnectorScopedContextProvider.GetConnectorScopedContext(connectorIdentifier);
                connectorScope.OnConnectorPacketsChanged += ConnectorPacketsChangedHandler;

                _connectorsScopedContexts.Add(connectorIdentifier, connectorScope);
            }

            if (_connectorsScopedContexts.Count == 0)
            {
                _selectedIdentifier = null;
            }
            else if (_selectedIdentifier is not { } selectedIdentifier || !_connectorsScopedContexts.ContainsKey(selectedIdentifier))
            {
                UpdateSelectedConnector(_connectorsScopedContexts.Keys.First());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to initialize connectors.");
        }
        finally
        {
            StateHasChanged();
        }
    }

    private void ActiveConnectorsChanged()
    {
        _ = InvokeAsync(InitializeConnectors);
    }

    private void ConnectorPacketsChangedHandler(ConnectorIdentifier connectorIdentifier, ConnectorPacketsChangedDto changed)
    {
        if (connectorIdentifier == _selectedIdentifier)
        {
            _ = StateHasChangedOnUiThread();
        }
    }

    private void UpdateSelectedConnector(ConnectorIdentifier connectorIdentifier)
    {
        _selectedPackets.Clear();
        _selectedIdentifier = connectorIdentifier;
        _selectedViewIndex = 0;
    }

    private void SelectedViewChanged(int selectedIndex)
    {
        _selectedViewIndex = selectedIndex;
        _packetGrids.Clear();
        _selectedPackets.Clear();

        ApplyStateToUrl();
    }

    private async Task OnSelectedConnectorChanged(ConnectorIdentifier selectedConnectorState)
    {
        UpdateSelectedConnector(selectedConnectorState);

        foreach(var grid in _packetGrids)
        {
            await grid.ResetGridAsync();
        }

        if (!_liveMode)
        {
            await StopOfflineMode();
            await EnterOfflineMode();
        }

        // Allow the page to refresh (so the new tabs are rendered) before setting the active tab
        // TODO: Should find a better way, maybe always keep the tabs rendered, but not displayed.
        await Task.Delay(1);
        _tabs?.ActivatePanel(0);

        ApplyStateToUrl();
    }

    private async Task ModeChanged(bool liveMode)
    {
        try
        {
            _liveMode = liveMode;

            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];

            foreach (var packetGrid in _packetGrids)
            {
                await packetGrid.ReloadGrid(liveMode);
            }

            if (_liveMode)
            {
                await StopOfflineMode();
            }
            else
            {
                await EnterOfflineMode();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to change mode.");
        }
        finally
        {
            ApplyStateToUrl();
        }
    }

    private async Task OpenPacketFilterDialog()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var connectorScope = _connectorsScopedContexts[selectedIdentifier];

            var dialog = await DialogService.Create<PacketFilterDialog>()
                .Title(Localizer["PacketFilter"])
                .Options(new DialogOptions() { MaxWidth = MaxWidth.Small, CloseOnEscapeKey = true, CloseButton = true })
                .Param(x => x.StartDate, connectorScope.FilterOptions.StartDateTime)
                .Param(x => x.EndDate, connectorScope.FilterOptions.EndDateTime)
                .Param(x => x.CurrentColumnFilterOptions, connectorScope.FilterOptions.ColumnFilters.ToList())
                .Param(x => x.CustomFilters, connectorScope.GetCustomFilters())
                .ShowAsync();

            var result = await dialog.Result;
            if (!result.Canceled && result.Data is PacketFilter dateTimeRange)
            {
                connectorScope.ApplyFilter(dateTimeRange.Start, dateTimeRange.End, dateTimeRange.PacketColumFilters);

                _selectedPackets.Clear();

                if (!_liveMode)
                {
                    await StopOfflineMode();
                    await EnterOfflineMode();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to open Filter Dialog.");
        }
        finally
        {
            ApplyStateToUrl();
        }
    }

    private async Task OpenActionDialog()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];
            var channels = GetChannelsFromView(selectedIdentifier);
            var selectedChannels = channels;

            var uiViewConfig = selectedConnectorScopedContext.GetUIViewConfig();
            var connectors = uiViewConfig.Connectors.Any()
                ? selectedConnectorScopedContext.GetUIViewConfig().Connectors.Select(c => new ConnectorIdentifier(selectedIdentifier.Instance, c)).ToImmutableArray()
                : [selectedIdentifier];

            var dialog = await DialogService.Create<ActionDialog>()
                .Title(Localizer["Action"])
                .Options(new DialogOptions() { MaxWidth = MaxWidth.Small, CloseOnEscapeKey = true, CloseButton = true })
                .Param(x => x.Channels, channels)
                .Param(x => x.SelectedChannels, selectedChannels)
                .Param(x => x.SelectedConnectors, connectors)
                .Param(x => x.Packets, selectedConnectorScopedContext.Packets.ToImmutableList())
                .Param(x => x.SelectedPackets, _selectedPackets)
                .Param(x => x.ConnectorIdentifier, selectedIdentifier)
                .Param(x => x.StartDateTime, selectedConnectorScopedContext.FilterOptions.StartDateTime)
                .Param(x => x.EndDateTime, selectedConnectorScopedContext.FilterOptions.EndDateTime)
                .Param(x => x.IsAdministrator, _isAdministrator)
                .ShowAsync();

            var result = await dialog.Result;
            if (result.Canceled)
            {
                return;
            }

            if (result.Data is ActionDialog.ActionImport import)
            {
                await UploadPacketsFile(import.File);
            }
            else if (result.Data is ActionDialog.ActionExportViewer exportViewer)
            {
                _liveMode = false;
                await UploadJsonViewFile(exportViewer.File);
            }
            else if (result.Data is ActionDialog.ActionStopSelected)
            {
                await StopSelected();
            }
            else if (result.Data is ActionDialog.ActionDeleteSelected)
            {
                await DeleteSelected();
            }
            else if (result.Data is ActionDialog.ActionDeleteAll)
            {
                _selectedPackets = [.. selectedConnectorScopedContext.Packets];
                await DeleteSelected();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not open Action Dialog.");
        }
    }

    private async Task OpenSequenceDialog()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];

            var connectorPackets = selectedConnectorScopedContext.Packets.ToArray();

            if (connectorPackets.Length == 0)
            {
                return;
            }

            long minId, maxId;
            if (_selectedPackets.Count > 0)
            {
                minId = _selectedPackets.Min(p => p.Id);
                maxId = _selectedPackets.Max(p => p.Id);
            }
            else
            {
                minId = connectorPackets.Min(p => p.Id);
                maxId = connectorPackets.Max(p => p.Id);
            }

            var channels = GetChannelsFromView(selectedIdentifier);
            var selectedChannels = channels;

            var dialog = await DialogService.Create<SequenceOptionsDialog>()
                .Title("Sequence interval")
                .Options(new DialogOptions() { MaxWidth = MaxWidth.Small, CloseOnEscapeKey = true, CloseButton = true })
                .Param(x => x.SequenceStartId, minId)
                .Param(x => x.SequenceEndId, maxId)
                .Param(x => x.Packets, connectorPackets)
                .Param(x => x.SelectedPackets, _selectedPackets)
                .Param(x => x.Channels, channels)
                .Param(x => x.SelectedChannels, selectedChannels)
                .ShowAsync();
            var result = await dialog.Result;

            if (!result.Canceled)
            {
                switch (result.Data)
                {
                    case SequenceOptionsDialog.SequenceIntervalSelect interval:
                        {
                            _selectedPackets = connectorPackets
                                .Where(p =>
                                    p.Id >= interval.SequenceStartId &&
                                    p.Id <= interval.SequenceEndId &&
                                    interval.SequenceChannel.Contains(p.Channel))
                                .ToHashSet();
                            break;
                        }

                    case SequenceOptionsDialog.SequenceIntervalDeselect:
                        _selectedPackets.Clear();
                        break;
                    case SequenceOptionsDialog.SequenceIntervalRun:
                        await RunSelected();
                        break;
                    case SequenceOptionsDialog.SequenceIntervalDelete:
                        await DeleteSelected();
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Could not open Sequence Dialog.");
        }
    }

    private async Task RunSelected()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorContext = _connectorsScopedContexts[selectedIdentifier];

            await selectedConnectorContext.ResendPacketsAsync(_selectedPackets);

            _selectedPackets.Clear();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to run selected packets.");
        }
    }

    private async Task StopSelected()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorContext = _connectorsScopedContexts[selectedIdentifier];

            await selectedConnectorContext.StopPacketsAsync(_selectedPackets);

            _selectedPackets.Clear();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to stop selected packets.");
        }
    }

    private async Task DeleteSelected()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorContext = _connectorsScopedContexts[selectedIdentifier];

            await selectedConnectorContext.DeletePacketsAsync(_selectedPackets);

            _selectedPackets.Clear();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete selected packets.");
        }
    }

    private HashSet<string> GetChannelsFromView(ConnectorIdentifier connectorIdentifier)
    {
        var selectedConnectorContext = _connectorsScopedContexts[connectorIdentifier];

        return selectedConnectorContext.GetUIViewConfig().Views.SelectMany(
            v => v.Value.Channels.Select(c => c.Channel)).ToHashSet();
    }

    private async Task UploadPacketsFile(IBrowserFile file)
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            ConnectorPacketsExportDto exportData;
            try
            {
                using var importFs = file.OpenReadStream();
                exportData = await JsonSerializer.DeserializeAsync<ConnectorPacketsExportDto>(importFs) ?? throw new Exception("Json is null");
            }
            catch
            {
                Snackbar.Add(Localizer["JsonOpenError"], Severity.Error);
                throw;
            }

            var selectedConnectorContext = _connectorsScopedContexts[selectedIdentifier];

            var importData = new ConnectorPacketsExportDto(exportData.Packets);

            var checkImport = await selectedConnectorContext.ImportPacketsAsync(importData, false);
            var success = false;

            if (!checkImport)
            {
                var dialog = await DialogService.Create<ImportWarningDialog>()
                    .Title(Localizer["ImportWarningMessage"])
                    .Options(new DialogOptions() { MaxWidth = MaxWidth.Small, CloseOnEscapeKey = true, CloseButton = true })
                    .ShowAsync();
                var result = await dialog.Result;

                if (!result.Canceled)
                {
                    success = await selectedConnectorContext.ImportPacketsAsync(importData, true);
                }
            }

            if (success)
            {
                Snackbar.Add(Localizer["ImportSuccessfulMessage"], Severity.Success);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upload packets file.");
        }
    }

    private async Task UploadJsonViewFile(IBrowserFile file)
    {
        try
        {
            ConnectorPacketsExportDto exportData;
            try
            {
                using var importFs = file.OpenReadStream();
                exportData = await JsonSerializer.DeserializeAsync<ConnectorPacketsExportDto>(importFs) ?? throw new Exception("Json is null");
            }
            catch
            {
                Snackbar.Add(Localizer["JsonFormatError"], Severity.Error);
                throw;
            }

            if (exportData.Packets.Length == 0)
            {
                Snackbar.Add(Localizer["JsonEmptyError"], Severity.Error);
                return;
            }

            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];
            var jsonViewerScopedConnectorContext = new JsonViewerScopedContext(JsonViewerIdentifier, exportData, selectedConnectorScopedContext.FilterOptions.StartDateTime, selectedConnectorScopedContext.FilterOptions.EndDateTime);

            _connectorsScopedContexts.Add(JsonViewerIdentifier, jsonViewerScopedConnectorContext);

            _selectedIdentifier = JsonViewerIdentifier;

            _selectedPackets.Clear();

            _isExportViewerMode = true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to upload json view file.");
        }
    }

    private async Task CloseJsonViewFile()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            _selectedIdentifier = _connectorsScopedContexts.Keys.First();
            _connectorsScopedContexts.Remove(JsonViewerIdentifier);

            _isExportViewerMode = false;

            _selectedViewIndex = 0;

            if (!_liveMode)
            {
                await StopOfflineMode();
                await EnterOfflineMode();
            }
        }
        catch(Exception ex)
        {
            Logger.LogError(ex, "Failed to close json view file.");
        }
    }

    private void HandlePacketRowClick(PacketDto packet)
    {
        if (!_isAdministrator || _isExportViewerMode)
        {
            return;
        }

        if (_selectedPackets.RemoveWhere(p => p.Id == packet.Id) == 0)
        {
            _selectedPackets.Add(packet);
        }

        StateHasChanged();
    }

    private async Task HandlePacketResendClick(PacketDto packet)
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];

            await selectedConnectorScopedContext.ResendPacketsAsync([packet]);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to resend packet.");
        }
    }

    private async Task HandlePacketGridScroll(object sender, PacketGrid.GridScrollEventArgs gridScrollEventArgs)
    {
        foreach (var packetGrid in _packetGrids)
        {
            if (packetGrid == sender)
            {
                continue;
            }
            if (packetGrid.Packets.Count == 0)
            {
                continue;
            }
            try
            {
                var closestPacket = packetGrid.Packets.Aggregate((x, y) => Math.Abs(x.Id - gridScrollEventArgs.PacketId) < Math.Abs(y.Id - gridScrollEventArgs.PacketId) ? x : y);

                await packetGrid.ScrollToPacket(closestPacket.Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "HandlePacketGridScroll");
            }
        }
    }

    private async Task HandleFilterRunnerStopped()
    {
        await StopOfflineMode();
    }

    private async Task HandleFilterRunnerStarted()
    {
        await EnterOfflineMode();
    }

    private async Task EnterOfflineMode()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier) { return; }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];

            await selectedConnectorScopedContext.StartFilterRunner();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to start offline mode.");
        }
    }

    private async Task StopOfflineMode()
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier) { return; }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];
            await selectedConnectorScopedContext.StopFilterRunner();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to stop offline mode.");
        }
    }

    private async Task RemoveColumnFilter(MudChip filterChip)
    {
        try
        {
            if (_selectedIdentifier is not { } selectedIdentifier)
            {
                throw new InvalidOperationException("No connector selected");
            }

            var selectedConnectorScopedContext = _connectorsScopedContexts[selectedIdentifier];

            var newColumnFilters = selectedConnectorScopedContext.FilterOptions.ColumnFilters.ToList();
            newColumnFilters.Remove((PacketColumnFilter)filterChip.Value);

            selectedConnectorScopedContext.ApplyFilter(columnFilters: newColumnFilters);

            _selectedPackets.Clear();

            if (!_liveMode)
            {
                await StopOfflineMode();
                await EnterOfflineMode();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to remove filter chip.");
        }
        finally
        {
            ApplyStateToUrl();
        }
    }

    private Dictionary<string, ColumnConfig>? GetColumnsConfig()
    {
        var columns = new Dictionary<string, ColumnConfig>();

        if ((!_isExportViewerMode ? _selectedIdentifier : JsonViewerIdentifier) is { } selectedIdentifier)
        {
            var uiViewConfig = _connectorsScopedContexts[selectedIdentifier].GetUIViewConfig();
            var selectedView = (string)_tabs?.ActivePanel.Tag!;

            if (selectedView is { } viewName && uiViewConfig.Views.TryGetValue(viewName, out var view))
            {
                if (view.Channels.FirstOrDefault()?.Columns is not null)
                {
                    foreach (var column in view.Channels.First().Columns)
                    {
                        columns.Add(column.Key, column.Value);
                    }
                }
                if (view.Columns is not null)
                {
                    foreach (var column in view.Columns)
                    {
                        if (columns.TryGetValue(column.Key, out var value))
                        {
                            value.Visible = value.Visible || column.Value.Visible;
                            value.Format ??= column.Value.Format;
                            value.Width ??= column.Value.Width;
                            value.SortDirection ??= column.Value.SortDirection;
                        }
                        else
                        {
                            columns.Add(column.Key, column.Value);
                        }
                    }
                }
            }

            if (uiViewConfig.Columns is not null)
            {
                foreach (var column in uiViewConfig.Columns)
                {
                    if (columns.TryGetValue(column.Key, out var value))
                    {
                        value.Visible = value.Visible || column.Value.Visible;
                        value.Format ??= column.Value.Format;
                        value.Width ??= column.Value.Width;
                        value.SortDirection ??= column.Value.SortDirection;
                    }
                    else
                    {
                        columns.Add(column.Key, column.Value);
                    }
                }
            }
        }

        return columns.Count == 0 ? null : columns;
    }

    private void ConfigureSnackbar()
    {
        Snackbar.Configuration.SnackbarVariant = Variant.Filled;
        Snackbar.Configuration.ClearAfterNavigation = true;
        Snackbar.Configuration.VisibleStateDuration = 3000;
        Snackbar.Configuration.PositionClass = Defaults.Classes.Position.TopCenter;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var connectorScope in _connectorsScopedContexts.Values)
        {
            await connectorScope.DisposeAsync();
        }

        if (_refreshStopSource != null)
        {
            await _refreshStopSource.CancelAsync();
            _refreshStopSource.Dispose();
        }

        ConnectorRegistry.ActiveConnectorsChanged -= ActiveConnectorsChanged;

        await StopOfflineMode();
    }

    private enum RefreshMode { Any, OnlyAuto, OnlyManual }
}
