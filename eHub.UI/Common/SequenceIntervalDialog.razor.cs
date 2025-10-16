using eHub.Contracts.UIConfig;
using eHub.UI.Localization;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace eHub.UI.Common;

public partial class SequenceIntervalDialog : FrameworkComponent
{
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }

    [CascadingParameter] public required MudDialogInstance MudDialog { get; set; }
	[Parameter] public long SequenceStartId { get; set; }
	[Parameter] public long SequenceEndId { get; set; }
	[Parameter] public required IReadOnlyList<PacketDto> Packets { get; set; }
	[Parameter] public required IReadOnlySet<string> Channels { get; set; }
	[Parameter] public required IReadOnlySet<string> SelectedChannels { get; set; }

	private IEnumerable<string> _selectedChannels = Enumerable.Empty<string>();

	protected override void OnParametersSet()
	{
		_selectedChannels = SelectedChannels;
	}

	private void Submit() => MudDialog.Close(DialogResult.Ok(new SequenceOptionsDialog.SequenceIntervalSelect(SequenceStartId, SequenceEndId, _selectedChannels.ToHashSet())));
	private void Deselect() => MudDialog.Close(DialogResult.Ok(new SequenceOptionsDialog.SequenceIntervalDeselect()));
	private void Cancel() => MudDialog.Cancel();
	private void SelectAll()
	{
		SequenceStartId = Packets.Min(p => p.Id);
		SequenceEndId = Packets.Max(p => p.Id);
	}
}
