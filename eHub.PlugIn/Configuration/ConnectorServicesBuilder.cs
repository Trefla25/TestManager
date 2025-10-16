using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eHub.PlugIn.Configuration;

/// <summary>
/// Provides access to the dependency injection container and configuration when setting up a connector's dependencies.
/// </summary>
public sealed class ConnectorServicesBuilder(IServiceCollection services, IConfiguration configuration)
{
    /// <summary>
    /// Gets the service collection into which the connector can register its services.
    /// </summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>
    /// Gets the configuration available to the connector for reading settings and binding options.
    /// This is the merged configuration built for the specific connector instance.
    /// </summary>
    public IConfiguration Configuration { get; } = configuration;
}
