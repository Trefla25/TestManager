using eHub.PlugIn;

namespace eHub.Scripting.Connectors;

public class TypeUtilities
{
    public static bool IsPacketTransfer(Type type) => type.IsAssignableTo(typeof(IPacketTransfer));
    public static bool IsHttpConnector(Type type) => type.IsAssignableTo(typeof(IHttpConnector));
    public static bool IsHttpPacketTransfer(Type type) => type.IsAssignableTo(typeof(IHttpPacketTransfer));
    public static bool IsConnectorConfigurator(Type type) => type.IsAssignableTo(typeof(IConnectorConfigurator));
    public static bool IsDependencySetup(Type type) => type.IsAssignableTo(typeof(ISetupDependenciesConnector));
}
