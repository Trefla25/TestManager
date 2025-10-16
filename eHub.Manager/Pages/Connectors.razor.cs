using eHub.Contracts;
using eHub.Contracts.Manager;
using eHub.Manager.Localization;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;
using eMessenger;

namespace eHub.Manager.Pages;

public sealed partial class Connectors : IDisposable
{
    private HttpClient? _httpClient;
    private ConnectorSummary? _connectorsSummaries;
    private bool _loading = true;
    private string? _lastErrorMessage;

    private IRegistrationToken _registrationToken = NullRegistrationToken.Instance;

    private System.Timers.Timer? _timer;

    [Parameter]
    public required string InstanceName { get; set; }

    [Inject]
    public required NavigationManager NavigationManager { get; init; }

    [Inject]
    public required IHttpClientFactory HttpClientFactory { get; init; }

    [Inject]
    public required IScopedMessenger Messenger { get; init; }

    [Inject]
    public required ILogger<Connectors> Logger { get; init; }

    [Inject]
    public required IStringLocalizer<Language> Localizer { get; init; }

    protected override async Task OnInitializedAsync()
    {
        _httpClient = HttpClientFactory.CreateClient();

        _registrationToken += await Messenger.ListenAsync<ConnectorStatus>(ConnectorContract.EHub_RequestConnectorStatus(InstanceName), HandleConnectorStatus);

        _timer = new System.Timers.Timer(750);
        _timer.Elapsed += async (sender, e) =>
        {
            await InvokeAsync(async () =>
            {
                await GetAllFoundConnectors();
                StateHasChanged();
            });
        };

        _timer.Start();

        await base.OnInitializedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await GetAllFoundConnectors();
        await base.OnParametersSetAsync();
    }

    public async Task GetAllFoundConnectors()
    {
        _lastErrorMessage = null;

        if (_httpClient == null)
        {
            return;
        }

        try
        {
            var response = await Messenger.AskAsync<ConnectorInstance>(ConnectorContract.EHub_RequestConnectorSummary(InstanceName))
                .FirstOrDefaultResponse();

            if (response is null)
            {
                _connectorsSummaries = null;
                return;
            }

            if (_connectorsSummaries is null)
            {
                _connectorsSummaries = response.ConnectorSummary;
            }
            else
            {
                // TODO recheck what this is doing
                _connectorsSummaries = response.ConnectorSummary with { ConnectorStatus = _connectorsSummaries.ConnectorStatus };
            }
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

    private void HandleConnectorStatus(ConnectorStatus connectorStatus)
    {
        if (connectorStatus is null || _connectorsSummaries is null)
        {
            return;
        }

        for (var i = 0; i < _connectorsSummaries.ConnectorStatus.Count; i++)
        {
            if (_connectorsSummaries.ConnectorStatus[i] is not null
                && _connectorsSummaries.ConnectorStatus[i].ConnectorName == connectorStatus.ConnectorName)
            {
                _connectorsSummaries.ConnectorStatus[i] = connectorStatus;
                InvokeAsync(StateHasChanged);
                return;
            }
        }

        _connectorsSummaries.ConnectorStatus.Add(connectorStatus);
        InvokeAsync(StateHasChanged);
    }

    private async Task StartConnector(string connectorKey)
    {
        await Messenger.SendAsync(ConnectorContract.EHub_Action_Start_Connector(InstanceName), new ConnectorByName(connectorKey));
    }

    private async Task StopConnector(string connectorKey)
    {
        await Messenger.SendAsync(ConnectorContract.EHub_Action_Stop_Connector(InstanceName), new ConnectorByName(connectorKey));
    }

    private bool IsConnectorRunning(string templateName)
    {
        if (_connectorsSummaries is null)
        {
            return false;
        }

        return _connectorsSummaries.ActiveConnectors.Any(x => x.TemplateName == templateName);
    }

    private string GetScriptFileByTemplateName(string templateName)
    {
        if (_connectorsSummaries is null)
        {
            return "/";
        }

        var scriptFile = _connectorsSummaries.ActiveConnectors
            .Where(x => x.TemplateName == templateName).FirstOrDefault();

        if (scriptFile is null)
        {
            return "/";
        }

        return scriptFile.ScriptFile ?? "/";
    }

    private string GetStatusByTemplateName(string connectorName)
    {
        if (_connectorsSummaries is null)
        {
            return "Not running";
        }

        return IsConnectorRunning(connectorName) is true ? "Running" : "Not running";
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _registrationToken.Dispose();
        _timer?.Dispose();
    }
}
