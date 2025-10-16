using eHub.Contracts.UIConfig;
using eHub.UI.Localization;
using eHub.UI.Util;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace eHub.UI.Common;

public partial class SequenceOptionsDialog : FrameworkComponent
{
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }
    [Inject] public required IDialogService DialogService { get; init; }

    [CascadingParameter] public required MudDialogInstance MudDialog { get; set; }
    [Parameter] public long SequenceStartId { get; set; }
    [Parameter] public long SequenceEndId { get; set; }
    [Parameter] public required IReadOnlyList<PacketDto> Packets { get; set; }
    [Parameter] public required IReadOnlySet<PacketDto> SelectedPackets { get; set; }
    [Parameter] public required IReadOnlySet<string> Channels { get; set; }
    [Parameter] public required IReadOnlySet<string> SelectedChannels { get; set; }

    private async Task OpenSequenceIntervalDialog()
    {
        if (!Packets.Any())
        {
            return;
        }

        var dialog = await DialogService.Create<SequenceIntervalDialog>()
            .Title(Localizer["SequenceInterval"])
            .Options(new DialogOptions() { MaxWidth = MaxWidth.Small, CloseOnEscapeKey = true, CloseButton = true })
            .Param(x => x.SequenceStartId, SequenceStartId)
            .Param(x => x.SequenceEndId, SequenceEndId)
            .Param(x => x.Packets, Packets)
            .Param(x => x.Channels, Channels)
            .Param(x => x.SelectedChannels, SelectedChannels)
            .ShowAsync();

        var result = await dialog.Result;
        if (result.Canceled)
        {
            return;
        }

        MudDialog.Close(result);
    }
    void ToggleFullWidth()
    {
        MudDialog.Options.FullWidth = !(MudDialog.Options.FullWidth ?? true);
        MudDialog.SetOptions(MudDialog.Options);
    }
    private void RunSequence() => MudDialog.Close(DialogResult.Ok(new SequenceIntervalRun()));
    private void DeleteSequence() => MudDialog.Close(DialogResult.Ok(new SequenceIntervalDelete()));
    private void Cancel() => MudDialog.Cancel();

    public record SequenceIntervalSelect(long SequenceStartId, long SequenceEndId, HashSet<string> SequenceChannel);
    public record SequenceIntervalDeselect();
    public record SequenceIntervalRun();
    public record SequenceIntervalDelete();
}
