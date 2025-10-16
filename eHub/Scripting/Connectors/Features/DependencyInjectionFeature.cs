using System.Reflection;
using eHub.PlugIn;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Features;

public class DependencyInjectionFeature
{
    public static void InvokeDependencySetup(Type connectorType, IServiceCollection connectorServices, IConfiguration configuration)
    {
        if (!TypeUtilities.IsDependencySetup(connectorType))
        {
            return;
        }

        var builder = new ConnectorServicesBuilder(connectorServices, configuration);
        var setupMethod = connectorType.GetMethod(nameof(ISetupDependenciesConnector.SetupDependencies),
            BindingFlags.Static | BindingFlags.Public, [typeof(ConnectorServicesBuilder)])!;
        setupMethod.Invoke(null, [builder]);
    }
}
