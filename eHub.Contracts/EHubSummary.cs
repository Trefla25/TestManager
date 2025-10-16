using eHub.Contracts.Manager;

namespace eHub.Contracts;

public class EHubSummary
{
    public string Instance { get; set; }
    public string? InstanceDisplayName { get; set; }
    public ConnectorSummary Connectors { get; set; }
    public ControllerInstance Controllers { get; set; }
}
