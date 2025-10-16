using System.Text.Json;
using BlazorMonaco.Editor;
using eHub.PlugIn.UI;
using eHub.UI.Localization;
using eHub.UI.Util;
using eController.WebUI.Utility.DynamicComponent;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace eHub.UI.Common;

public partial class DataDialog : FrameworkComponent
{
    [Inject] public required IStringLocalizer<Language> Localizer { get; init; }
    [Inject] public required ILogger<DataDialog> Logger { get; init; }
    [Inject] public required AuthenticationStateProvider AuthenticationStateProvider { get; init; } = default!;

    [CascadingParameter] public required MudDialogInstance MudDialog { get; set; }
    [Parameter] public string? Data { get; set; }
    [Parameter] public string? DataType { get; set; }
    [Parameter] public bool CanResend { get; set; }
    [Parameter] public Action<string>? OnPacketResendClick { get; set; }


    private bool IsValueChanged => _oldValue != _value;
    private bool IsFullscreen => MudDialog.Options.FullScreen == true;
    private string DialogContentHeight => IsFullscreen ? "calc(100vh - 178px)" : "calc(100vh - 238px)";

    private StandaloneCodeEditor? Editor { get; set; }

    private bool _isAdministrator;
    private bool _isEditMode;
    private string? _oldValue;
    private string? _value;
    private string? _language;

    private void Resend()
    {
        OnPacketResendClick?.Invoke(_value ?? string.Empty);
        MudDialog.Close();
    }

    private void Close() => MudDialog.Close();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        _isAdministrator = authState.User?.Claims.Any(c => c.Value == "Administrator") ?? false;
    }

    protected override void OnParametersSet()
    {
        base.OnParametersSet();

        _language = DataType ?? UIDataTypes.Plaintext;

        if (DataType == UIDataTypes.Xml && XmlUtil.TryParse(Data, out var xmlDocument))
        {
            _oldValue = _value = xmlDocument?.ToString();
        }
        else if (DataType == UIDataTypes.Json && JsonUtil.TryParse(Data, out var jsonDocument))
        {
            string formattedJson = JsonSerializer.Serialize(jsonDocument, new JsonSerializerOptions() { WriteIndented = true });
            _oldValue = _value = formattedJson;
        }
        else
        {
            _oldValue = _value = Data;
            _language = UIDataTypes.Plaintext;
        }
    }

    private StandaloneEditorConstructionOptions EditorConstructionOptions(StandaloneCodeEditor editor)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = _language,
            ReadOnly = true
        };
    }

    private async Task EditorOnDidInit()
    {
        if (Editor == null)
        {
            return;
        }

        var model = await Editor.GetModel();
        await model.SetValue(_value);
        await Editor.Trigger("", "editor.action.formatDocument");
        StateHasChanged();
    }

    private async Task OnDidChangeModelContent(ModelContentChangedEvent e)
    {
        if (Editor == null)
        {
            return;
        }

        var model = await Editor.GetModel();
        _value = await model.GetValue(EndOfLinePreference.TextDefined, false);
    }

    private async Task EnterEditMode()
    {
        if (Editor == null || !_isAdministrator)
        {
            return;
        }

        await Editor.UpdateOptions(new EditorUpdateOptions { ReadOnly = false });
        _isEditMode = true;
    }

    private async Task ExitEditMode()
    {
        if (Editor == null)
        {
            return;
        }

        await Editor.UpdateOptions(new EditorUpdateOptions { ReadOnly = true });
        _isEditMode = false;
    }

    private void ToggleFullscreen()
    {
        MudDialog.Options.FullScreen = !(MudDialog.Options.FullScreen ?? false);
        MudDialog.SetOptions(MudDialog.Options);
    }

    private async Task DiscardChanges()
    {
        if (Editor == null)
        {
            return;
        }

        var model = await Editor.GetModel();
        _value = _oldValue;
        await model.SetValue(_value);
        await Editor.Trigger("", "editor.action.formatDocument");
    }
}
