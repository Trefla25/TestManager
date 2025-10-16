using eHub.Contracts;
using eMessenger;

namespace eHub.Scripting.Connectors;

public class ConnectorMetadata(MessagingContext messagingContext, string templateName, string connectorType)
{
    public string TemplateName { get; } = templateName;
    public string ConnectorType { get; } = connectorType;
    public ConnectorIdentifier ConnectorIdentifier { get; } = new(messagingContext.OwnId.ToString(), templateName);
}
