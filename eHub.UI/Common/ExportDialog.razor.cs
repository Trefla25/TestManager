using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using eHub.Contracts;
using eHub.UI.Localization;
using eController.WebUI.Utility.DynamicComponent;
using eMessenger;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace eHub.UI.Common;

public partial class ExportDialog : FrameworkComponent
{
    [Inject] public required IMessenger Messenger { get; init; }
    [Inject] public required NavigationManager NavigationManager { get; init; }
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }

    [CascadingParameter] public required MudDialogInstance MudDialog { get; set; }
    [Parameter] public required IReadOnlySet<string> Channels { get; set; }
    [Parameter] public required IReadOnlySet<string> SelectedChannels { get; set; }
    [Parameter] public required ImmutableArray<ConnectorIdentifier> SelectedConnectors { get; set; }
    [Parameter] public ConnectorIdentifier ConnectorIdentifier { get; set; }
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; }

    private IEnumerable<string> _selectedChannels = Enumerable.Empty<string>();
    private string title = "<Unknown>";

    protected override void OnParametersSet()
    {
        _selectedChannels = SelectedChannels;
        title = $"{DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss")} - {ConnectorIdentifier.ConnectorKey}_Export";
    }

    private void Export()
    {
        var filter = new PacketRequestDto(StartDateTime, EndDateTime, Channels: _selectedChannels.ToImmutableArray());

        string customConnectorIdentifier = "";

        foreach (var connector in SelectedConnectors)
        {
            customConnectorIdentifier += connector.ToString() + ";";
        }

        customConnectorIdentifier = customConnectorIdentifier.Remove(customConnectorIdentifier.Length - 1);

        var apiUrl = new StringBuilder()
            .Append($"api/connectors/export")
            .Append($"?connectorIdentifier={Uri.EscapeDataString(customConnectorIdentifier)}")
            .Append($"&title={Uri.EscapeDataString(title)}")
            .Append($"&filterJson={Uri.EscapeDataString(JsonSerializer.Serialize(filter))}");

        NavigationManager.NavigateTo(apiUrl.ToString(), forceLoad: true);

        MudDialog.Close(DialogResult.Ok(new ActionDialog.ActionExport()));
    }

    private void Cancel() => MudDialog.Cancel();
}
