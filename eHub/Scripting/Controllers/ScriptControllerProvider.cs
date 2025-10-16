using System.Reflection;
using ePlugin.Engine.Client;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace eHub.Scripting.Controllers;

/// <summary>
/// <inheritdoc cref="IPluginPublisher"/><br/>
/// From those plugins all API Controllers will be build and added into the ASP pipeline.<br/>
/// You can use the <see cref="DynamicActionProvider"/> to add and remove endpoints without restarting or rebuilding the pipeline.
/// </summary>
public class ScriptControllerProvider
{
    private readonly ILogger<ScriptControllerProvider> _logger;
    private readonly Func<ApplicationModel, IEnumerable<ControllerActionDescriptor>> _controllerActionDescriptorBuilder_Build;
    private readonly Func<IEnumerable<TypeInfo>, ApplicationModel> _applicationModelFactory_CreateApplicationModel;
    private readonly Dictionary<string, ControllerActionDescriptor[]> _managedControllers = [];

    /// <summary>Triggered after new controller actions have been loaded.</summary>
    public event EventHandler<IReadOnlyCollection<ControllerActionDescriptor>>? ControllersAdded;
    /// <summary>Triggered before controller actions will be unloaded.</summary>
    public event EventHandler<IReadOnlyCollection<ControllerActionDescriptor>>? ControllersRemoved;

    public ScriptControllerProvider(
        ILogger<ScriptControllerProvider> logger,
        IPluginPublisher scriptProvider,
        IServiceProvider serviceProvider)
    {
        _logger = logger;

        var assembly = typeof(ControllerModel).Assembly; // Microsoft.AspNetCore.Mvc.Core
        {
            var controllerBuilderType = assembly.GetTypeSmart("Microsoft.AspNetCore.Mvc.ApplicationModels.ControllerActionDescriptorBuilder")!;
            var buildMethod = controllerBuilderType.GetMethod("Build", BindingFlags.Static | BindingFlags.Public)!;
            _controllerActionDescriptorBuilder_Build
                = buildMethod.CreateDelegate<Func<ApplicationModel, IEnumerable<ControllerActionDescriptor>>>();
        }
        {
            var factoryType = assembly.GetTypeSmart("Microsoft.AspNetCore.Mvc.ApplicationModels.ApplicationModelFactory")!;
            var factory = serviceProvider.GetRequiredService(factoryType);
            var method = factoryType.GetMethod("CreateApplicationModel")!;
            _applicationModelFactory_CreateApplicationModel
                = (a) => (ApplicationModel)method.Invoke(factory, new[] { a })!;
        }

        scriptProvider.AfterPluginRemoved += ScriptProvider_ScriptRemoved;
        scriptProvider.SubscribeAndCatchUp(ScriptProvider_ScriptAdded);
    }

    private void ScriptProvider_ScriptAdded(PluginData plugin)
    {
        var controllers = plugin.Assemblies.SelectMany(asm => CreateActionDescriptors(asm)).ToArray();
        _managedControllers.Add(plugin.Name, controllers);
        ControllersAdded?.Invoke(this, controllers);
    }

    private void ScriptProvider_ScriptRemoved(PluginData plugin)
    {
        if (_managedControllers.Remove(plugin.Name, out var controllers))
        {
            ControllersRemoved?.Invoke(this, controllers);
        }
        else
        {
            _logger.LogError("Assembly source {PluginName} should be managed, but was not found.", plugin.Name);
        }
    }

    private IEnumerable<ControllerActionDescriptor> CreateActionDescriptors(Assembly assembly)
    {
        var controllerTypes = assembly.GetTypes().Where(IsController);
        var applicationModel = CreateApplicationModel(controllerTypes);
        return _controllerActionDescriptorBuilder_Build(applicationModel);
    }

    private ApplicationModel CreateApplicationModel(IEnumerable<Type> controllerTypes)
    {
        var typeInfos = controllerTypes.Select(it => it.GetTypeInfo());
        return _applicationModelFactory_CreateApplicationModel(typeInfos);
    }

    private static bool IsController(Type typeInfo)
    {
        if (!typeInfo.IsClass)
        {
            return false;
        }

        if (typeInfo.IsAbstract)
        {
            return false;
        }

        if (!typeInfo.IsPublic)
        {
            return false;
        }

        if (typeInfo.ContainsGenericParameters)
        {
            return false;
        }

        if (typeInfo.IsDefined(typeof(NonControllerAttribute)))
        {
            return false;
        }

        if (!typeInfo.Name.EndsWith("Controller", StringComparison.OrdinalIgnoreCase) && !typeInfo.IsDefined(typeof(ControllerAttribute)))
        {
            return false;
        }

        return true;
    }
}
