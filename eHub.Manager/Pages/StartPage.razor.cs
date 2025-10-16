using eController.Util;
using eController.WebUI.Utility.Menu;
using eHub.Manager.Localization;
using ElementLogic.Configuration.Server;
using eMessenger;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace eHub.Manager.Pages;

[MenuGroupItem(4711, "eHub", "power")]
[MenuItem("eHubManager", "table_view")]
[Route("/eHubManager")]
public sealed partial class StartPage : IDisposable
{
    private readonly HashSet<IRemoteConfigurationApp> _eHubInstances = [];

    private object? CurrentInstance
    {
        get => _currentInstance;
        set => _currentInstance = (IRemoteConfigurationApp)value;
    }

    private IRemoteConfigurationApp? _currentInstance;

    private Connectors? _connectors;
    private Controllers? _controllers;

    [Inject]
    public required IMessenger Messenger { get; init; }

    [Inject]
    public required IStringLocalizer<Language> Localizer { get; init; }

    [Inject]
    public required IEffortlessConfigurationServer ConfigurationServer { get; init; }

    protected override void OnInitialized()
    {
        ConfigurationServer.OnAppStatusChanged += HandleInstanceChange;
        SearchForEHubInstances();
    }

    private void HandleInstanceChange(object? sender, AppChangedEventArgs e)
    {
        _ = InvokeAsync(() =>
        {
            var changed = SearchForEHubInstances();
            if (changed)
            {
                StateHasChanged();
            }
        });
    }

    private bool SearchForEHubInstances()
    {
        var changed = CollectionUtil.Diff(
            _eHubInstances,
            ConfigurationServer.ConfigurationApps.Where(x => x.Descriptor.Type == "eHub" /*TODO use constant somewhere*/).ToHashSet(),
            add => _eHubInstances.Add(add),
            remove => _eHubInstances.Remove(remove));

        if (_currentInstance == null)
        {
            _currentInstance = _eHubInstances.FirstOrDefault();
            changed = changed || _currentInstance != null;
        }

        return changed;
    }

    public void Dispose()
    {
        ConfigurationServer.OnAppStatusChanged -= HandleInstanceChange;
    }
}
