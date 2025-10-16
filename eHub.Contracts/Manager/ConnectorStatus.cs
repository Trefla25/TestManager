namespace eHub.Contracts.Manager;

public sealed record ConnectorStatus(string ConnectorName, IReadOnlyDictionary<string, string> StatusDictionary);