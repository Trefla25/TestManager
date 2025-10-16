using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace eHub.Scripting.Controllers;

/// <summary></summary>
public static class ControllersExtensions
{
    /// <summary>Adds the script loader for API controllers.<br/>
    /// C# script files containing types annotated with <see cref="ApiControllerAttribute"/>
    /// will be loaded and added to the API pipeline of this ASP.NET server instance.<br/>
    /// Script paths can be defined in the config.</summary>
    public static void AddHubControllers(this IServiceCollection services)
    {
        services.AddSingleton<DynamicActionProvider>();
        services.TryAddTransient<ScriptControllerProvider>();
        services.AddSingleton<IActionDescriptorProvider>(provider => provider.GetRequiredService<DynamicActionProvider>());
        services.AddSingleton<IActionDescriptorChangeProvider>(provider => provider.GetRequiredService<DynamicActionProvider>());
        services.AddHostedService<ControllerManager>();
    }
}
