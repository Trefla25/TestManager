using System.Reflection;
using eHub.PlugIn;
using eHub.PlugIn.Configuration;

namespace eHub.Scripting.Connectors.Features;

public static class DependencyInjectionFeature
{
    public static void InvokeDependencySetup(Type connectorType, IServiceCollection connectorServices,
        IConfiguration configuration)
    {
        if (!TypeUtilities.IsDependencySetup(connectorType))
        {
            return;
        }

        var builder = new ConnectorServicesBuilder(connectorServices, configuration);

        var traverseType = connectorType;
        while (traverseType != null && traverseType != typeof(object))
        {
            var setupMethod = traverseType.GetMethod(nameof(ISetupDependenciesConnector.SetupDependencies),
                BindingFlags.Static | BindingFlags.Public, [typeof(ConnectorServicesBuilder)]);

            if (setupMethod != null)
            {
                setupMethod.Invoke(null, [builder]);
                return;
            }

            traverseType = traverseType.BaseType;
        }

        throw new InvalidOperationException(
            $"No static public method '{nameof(ISetupDependenciesConnector.SetupDependencies)}' found in type hierarchy of '{connectorType.FullName}'");
    }
}
