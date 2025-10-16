using System.Collections.Immutable;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eHub.UI.Localization;
using eHub.UI.Util;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace eHub.UI.Common;

public partial class ActionDialog : FrameworkComponent
{
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }
    [Inject] public required IDialogService DialogService { get; init; }
    [Inject] public required ILogger<ActionDialog> Logger { get; init; }

    [CascadingParameter] public required MudDialogInstance MudDialog { get; set; }
	[Parameter] public required IReadOnlySet<string> Channels { get; set; }
	[Parameter] public required IReadOnlySet<string> SelectedChannels { get; set; }
    [Parameter] public required ImmutableArray<ConnectorIdentifier> SelectedConnectors { get; set; }
    [Parameter] public required IReadOnlyList<PacketDto> Packets { get; set; }
	[Parameter] public required IReadOnlySet<PacketDto> SelectedPackets { get; set; }
	[Parameter] public ConnectorIdentifier ConnectorIdentifier { get; set; }
	[Parameter] public DateTime StartDateTime { get; set; }
	[Parameter] public DateTime EndDateTime { get; set; }
	[Parameter] public bool IsAdministrator { get; set; }

	private async Task OpenExportDialog()
	{
		var dialog = await DialogService.Create<ExportDialog>()
			.Title(Localizer["ExportOptions"])
			.Options(new DialogOptions() { MaxWidth = MaxWidth.Small, CloseOnEscapeKey = true, CloseButton = true })
			.Param(x => x.Channels, Channels)
			.Param(x => x.SelectedChannels, SelectedChannels)
			.Param(x => x.SelectedConnectors, SelectedConnectors)
			.Param(x => x.ConnectorIdentifier, ConnectorIdentifier)
			.Param(x => x.StartDateTime, StartDateTime)
			.Param(x => x.EndDateTime, EndDateTime)
			.ShowAsync();

        var result = await dialog.Result;
        if (result.Canceled)
        {
            return;
        }

        MudDialog.Close(result);
    }

    private void UploadPacketsFile(IBrowserFile file) => MudDialog.Close(DialogResult.Ok(new ActionImport(file)));

    private void UploadJsonViewFile(IBrowserFile file) => MudDialog.Close(DialogResult.Ok(new ActionExportViewer(file)));

    private void StopSelected() => MudDialog.Close(DialogResult.Ok(new ActionStopSelected()));
    private void DeleteSelected() => MudDialog.Close(DialogResult.Ok(new ActionDeleteSelected()));
    private void DeleteAll() => MudDialog.Close(DialogResult.Ok(new ActionDeleteAll()));

    void ToggleFullWidth()
    {
        MudDialog.Options.FullWidth = !(MudDialog.Options.FullWidth ?? true);
        MudDialog.SetOptions(MudDialog.Options);
    }

    private void Cancel() => MudDialog.Cancel();

    public record ActionExport();
    public record ActionImport(IBrowserFile File);
    public record ActionExportViewer(IBrowserFile File);
    public record ActionStopSelected();
    public record ActionDeleteSelected();
    public record ActionDeleteAll();
}
