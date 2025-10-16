using System.Collections.Immutable;
using eHub.Contracts;
using eHub.UI.Localization;
using eHub.UI.Services;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace eHub.UI.Common;

public partial class HistorySidePanel : FrameworkComponent
{
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }
    [Inject] public required IConnectorRegistry ConnectorState { get; init; }

    [Parameter] public ConnectorIdentifier? SelectedConnector { get; set; }
    [Parameter] public EventCallback<ConnectorIdentifier> SelectedConnectorChanged { get; set; }

    private bool _open = false;
    private bool _pin = false;

    private ImmutableArray<(string Key, ImmutableArray<(ConnectorIdentifier Ident, ConnectorUiData Ui)> Connectors)> _groupedConnectors = [];

    public bool Open
    {
        get => _open || _pin;
        set => _open = value;
    }

    private void ConnectorsChanged()
    {
        UpdateConnectors();
        _ = InvokeAsync(StateHasChanged);
    }

    protected override void OnInitialized()
    {
        ConnectorState.ActiveConnectorsChanged += ConnectorsChanged;
        UpdateConnectors();
        base.OnInitialized();
    }


    private void UpdateConnectors()
    {
        _groupedConnectors = ConnectorState.ActiveConnectors
            .GroupBy(c => c.Value.ConnectorType)
            .Select(g => (g.Key, g.OrderBy(x => x.Value.ConnectorName).Select(g => (g.Key, g.Value)).ToImmutableArray()))
            .OrderBy(g => g.Key)
            .ToImmutableArray();
    }

    private async Task SelectConnectorState(ConnectorIdentifier connectorIdent)
    {
        SelectedConnector = connectorIdent;
        await SelectedConnectorChanged.InvokeAsync(connectorIdent);
        StateHasChanged();
    }
}
