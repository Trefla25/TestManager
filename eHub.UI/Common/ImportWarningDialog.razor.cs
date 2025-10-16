using eHub.UI.Localization;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using MudBlazor;

namespace eHub.UI.Common;

public partial class ImportWarningDialog : FrameworkComponent
{
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }

    [CascadingParameter] public required MudDialogInstance MudDialog { get; set; }

    void Submit() => MudDialog.Close(DialogResult.Ok(true));
    void Cancel() => MudDialog.Cancel();
}
