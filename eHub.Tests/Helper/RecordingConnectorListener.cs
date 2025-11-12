using System.Collections.Immutable;
using eHub.Scripting.Connectors;

namespace eHub.Tests.Helper;

/// <summary>Testing utility to capture all events triggered by a connector.</summary>
internal class RecordingConnectorListener : IConnectorMessageHandler
{
    public List<(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)> Statuses { get; } = [];

    public void StatusChanged(string connectorName, IReadOnlyDictionary<string, string> statusDictionary)
    {
        Statuses.Add((connectorName, statusDictionary.ToImmutableDictionary()));
    }
}
