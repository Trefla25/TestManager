using System.Collections.Immutable;

namespace eHub.Contracts.Manager;

public sealed record ConnectorInstance(string EHubInstanceName, ConnectorSummary ConnectorSummary);

public sealed record ConnectorSummary
{
    /* For serialization */
    public ConnectorSummary() { }

    public ConnectorSummary(
        IEnumerable<ScriptInstanceDto> activeConnectors,
        IEnumerable<string> availableConnectorTypes,
        IEnumerable<NameConnectorTemplateDto> allConnectorTemplates)
    {
        ActiveConnectors = activeConnectors.ToImmutableArray();
        AvailableConnectorTypes = availableConnectorTypes.ToImmutableArray();
        AllConnectorTemplates = allConnectorTemplates.ToImmutableArray();
    }

    public List<ConnectorStatus> ConnectorStatus { get; set; } = [];

    public ImmutableArray<ScriptInstanceDto> ActiveConnectors { get; init; } = ImmutableArray<ScriptInstanceDto>.Empty;

    public ImmutableArray<string> AvailableConnectorTypes { get; init; } = ImmutableArray<string>.Empty;

    public ImmutableArray<NameConnectorTemplateDto> AllConnectorTemplates { get; init; } = ImmutableArray<NameConnectorTemplateDto>.Empty;
}

/// <summary>Api object for serialization. Describes a running connector</summary>
public sealed record ScriptInstanceDto(string TemplateName, string ConnectorType, IReadOnlyDictionary<string, string>? ConnectorInfo, string ScriptFile);

/// <summary>Api object for serialization. Describes a connector template</summary>
public sealed record NameConnectorTemplateDto(string TemplateName, string ConnectorType);
