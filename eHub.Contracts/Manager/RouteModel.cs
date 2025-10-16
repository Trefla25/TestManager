namespace eHub.Contracts.Manager;

public sealed record ControllerInstance(string EHubInstanceName, List<RouteModel> RouteModels);

public sealed record RouteModel(string? Template, IReadOnlyList<string> Verbs, string? DisplayName, string? ControllerName, string? ActionName);
