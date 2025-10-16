using eHub.PlugIn.Configuration;

namespace eHub.PlugIn;

/// <summary>Allows the connector to set up its dependencies in the ehub managed service collection.</summary>
public interface ISetupDependenciesConnector : IConnector
{
    /// <summary>
    /// Sets up the dependencies for the connector in the service collection.
    /// This method will be called whenever a new connector instance is being created.
    /// Dependencies registered here can be injected into the connector's constructor.
    /// </summary>
    static abstract void SetupDependencies(ConnectorServicesBuilder builder);
}
