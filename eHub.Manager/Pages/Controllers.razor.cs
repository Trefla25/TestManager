using eHub.Contracts;
using eHub.Contracts.Manager;
using eHub.Manager.Localization;
using eMessenger;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;
using MudBlazor;

namespace eHub.Manager.Pages;

public sealed partial class Controllers : IDisposable
{
    private HttpClient? _httpClient;
    private List<RouteModel>? _controllerRouteModels;
    private bool _loading = true;
    private string? _lastErrorMessage;

    private System.Timers.Timer? _timer;

    [Parameter]
    public required string InstanceName { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required ILogger<Controllers> Logger { get; init; }

    [Inject]
    public required IHttpClientFactory HttpClientFactory { get; init; }

    [Inject]
    public required IMessenger Messenger { get; init; }

    [Inject]
    public required IJSRuntime JSRuntime { get; init; }

    [Inject]
    public required ISnackbar Snackbar { get; init; }

    [Inject]
    public required IStringLocalizer<Language> Localizer { get; init; }

    protected override async Task OnInitializedAsync()
    {
        _httpClient = HttpClientFactory.CreateClient();

        _timer = new System.Timers.Timer(750);
        _timer.Elapsed += async (sender, e) =>
        {
            await InvokeAsync(async () =>
            {
                await GetAllFoundControllers();
                StateHasChanged();
            });
        };

        _timer.Start();

        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await GetAllFoundControllers();
        await base.OnParametersSetAsync();
    }

    public async Task GetAllFoundControllers()
    {
        _lastErrorMessage = null;

        if (_httpClient == null)
        {
            return;
        }

        try
        {
            var response = await Messenger.AskAsync<ControllerInstance?>(ConnectorContract.EHub_RequestControllerRouteModels(InstanceName))
                .FirstOrDefaultResponse();

            if (response is null)
            {
                _lastErrorMessage = "Host answered with empty or unknown data.";
                return;
            }

            _controllerRouteModels = response.RouteModels;
        }
        catch (Exception ex)
        {
            _lastErrorMessage = ex.Message;
            Logger.LogError(ex, "");
        }
        finally
        {
            _loading = false;
        }
    }

    private static string JoinVerbs(RouteModel routeModel)
    {
        return string.Join("\n", routeModel.Verbs);
    }

    private async Task CopyRouteToClipboard(string? context)
    {
        if (context is null)
        {
            return;
        }

        try
        {
            await JSRuntime.InvokeVoidAsync("setToClipboard", NavigationManager.BaseUri + context);
            Snackbar.Add("Route has been saved to clipboard.", Severity.Info);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message);
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _timer?.Dispose();
    }
}
