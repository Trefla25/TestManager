using eHub.Contracts;
using eHub.UI.Localization;
using eHub.UI.Services;
using eController.WebUI.PlugIn;
using eController.WebUI.Utility.WebLoader;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;

namespace eHub.UI;

public class Startup : ServerExtensionService
{
    public Startup(IServiceCollection services)
    {
        services.AddSingleton<MudLocalizer, CustomMudLocalizer>();

        services.AddSingleton<ConnectorRegistry>();
        services.AddSingleton<IConnectorRegistry>(x => x.GetRequiredService<ConnectorRegistry>());
        services.AddHostedService(x => x.GetRequiredService<ConnectorRegistry>());

        services.AddSingleton<ConnectorStateManager>();
        services.AddSingleton<IConnectorContextProvider>(x => x.GetRequiredService<ConnectorStateManager>());
        services.AddHostedService(x => x.GetRequiredService<ConnectorStateManager>());

        services.AddScoped<IConnectorScopedContextProvider, ConnectorScopedContextProvider>();

        services.AddSingleton<HistoryStateUrlService>();
    }

    public override async Task ConfigureScope(IServiceProvider services)
    {
        var jsLoader = services.GetRequiredService<IJsLoadService<Startup>>();

        await jsLoader.LoadCssAsync(
            "eHub.UI.bundle.scp.css");

        await jsLoader.LoadJsAsync(
            "javascript/helpers.js",
            "javascript/packetGrid.js");
    }
}
