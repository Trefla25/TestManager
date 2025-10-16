using eHub.Messaging;
using eHub.PlugIn;

namespace eHub.Scripting.Connectors;

/// <summary></summary>
public static class ConnectorExtensions
{
    /// <summary>Adds the script loader for IntegrationHub Connectors.<br/>
    /// C# script files containing types implementing <see cref="IConnector"/>
    /// will be loaded and instantiated when set in the template configuration.<br/>
    /// Script paths and templates can be defined in the config.</summary>
    public static void AddHubConnectors(this IServiceCollection services)
    {
        services.AddHostedService<MessengerEvents>();
        services.AddSingleton<IConnectorMessageHandler, EMessengerConnectorHandler>();
        services.AddSingleton<ConnectorManager>();
        services.AddHostedService(x => x.GetRequiredService<ConnectorManager>());
        services.AddSingleton<ISchedulerServiceProvider>(x => x.GetRequiredService<ConnectorManager>());
    }
}
