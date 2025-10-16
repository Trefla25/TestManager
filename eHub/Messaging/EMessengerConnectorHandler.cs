using eHub.Contracts.Manager;
using eHub.Scripting.Connectors;
using eMessenger;

namespace eHub.Messaging;

public sealed class EMessengerConnectorHandler(IMessenger messenger, MessagingContext messagingContext) : IConnectorMessageHandler
{
    private readonly string _ownInstanceId = messagingContext.OwnId.ToString();

    public async void StatusChanged(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)
    {
        await messenger.SendAsync(eHub.Contracts.ConnectorContract.EHub_RequestConnectorStatus(_ownInstanceId),
            new ConnectorStatus(connectorName, statusDictionary));
    }

    /// <summary>
    /// Called whenever the IntegrationHub setup has changed. This can include addition or removal of connector templates,
    /// update of watch or configuration paths, and various other config file changes.
    /// </summary>
    public void ConfigChanged()
    {

    }
}