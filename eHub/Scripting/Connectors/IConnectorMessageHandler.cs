using eHub.PlugIn;

namespace eHub.Scripting.Connectors;

/// <summary>Allows loosely coupled notifications from <see cref="IConnector"/>s.</summary>
public interface IConnectorMessageHandler
{
    /// <summary>Called whenever a <see cref="IConnector.UpdateStatus"/> is triggered.
    /// This information should usually just be used for debugging and monitoring.</summary>
    /// <param name="connectorName">Name of the connector that triggered the status update.</param>
    /// <param name="statusDictionary">The updated status dictionary.</param>
    public void StatusChanged(string connectorName, IReadOnlyDictionary<string, string> statusDictionary) { }

    /// <summary>
    /// Called whenever the IntegrationHub setup has changed. This can include addition or removal of connector templates,
    /// update of watch or configuration paths, and various other config file changes.
    /// </summary>
    public void ConfigChanged() { }
}
