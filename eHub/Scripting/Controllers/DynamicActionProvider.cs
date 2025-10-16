using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Primitives;

namespace eHub.Scripting.Controllers;

/// <summary>Implements a custom ASP.NET ApiController, allowing adding and removing ApiControllers at runtime.</summary>
public class DynamicActionProvider : IActionDescriptorProvider, IActionDescriptorChangeProvider
{
    private readonly List<ControllerActionDescriptor> _actions = [];
    private CancellationTokenSource _source;
    private CancellationChangeToken _token;

    public DynamicActionProvider()
    {
        _source = new CancellationTokenSource();
        _token = new CancellationChangeToken(_source.Token);
    }

    /// <inheritdoc />
    public int Order => -100;
    /// <inheritdoc />
    public void OnProvidersExecuted(ActionDescriptorProviderContext context) { }
    /// <inheritdoc />
    public void OnProvidersExecuting(ActionDescriptorProviderContext context)
    {
        foreach (var action in _actions)
        {
            context.Results.Add(action);
        }
    }

    /// <inheritdoc/>
    public IChangeToken GetChangeToken() => _token;

    /// <summary>Adds the controller endpoints to the ASP pipeline of this server.</summary>
    /// <param name="controllerActions">The api endpoint actions to add.</param>
    public void AddControllers(IEnumerable<ControllerActionDescriptor> controllerActions)
    {
        foreach (var action in controllerActions)
        {
            if (!action.EndpointMetadata.OfType<DynamicActionAttribute>().Any())
            {
                action.EndpointMetadata.Add(DynamicActionAttribute.Instance);
            }

            _actions.Add(action);
        }
        NotifyChanges();
    }

    /// <summary>Removes the controller endpoints from the ASP pipeline of this server.</summary>
    /// <param name="controllerActions">The api endpoint actions to add.</param>
    public void RemoveController(IEnumerable<ControllerActionDescriptor> controllerActions)
    {
        foreach (var action in controllerActions)
        {
            _actions.Remove(action);
        }
        NotifyChanges();
    }

    /// <summary>Triggers the current <see cref="IChangeToken"/> indicating route changes of currently loaded api controllers.</summary>
    private void NotifyChanges()
    {
        var old = Interlocked.Exchange(ref _source, new CancellationTokenSource());
        _token = new CancellationChangeToken(_source.Token);
        old.Cancel();
    }
}

/// <summary>Marker attribute which is applied to dynamically added ApiControllers to distinguish them from 
/// 'normally' loaded controllers. Controllers with this attribute can then be displayed on the webui.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal class DynamicActionAttribute : Attribute
{
    public static readonly DynamicActionAttribute Instance = new();
}