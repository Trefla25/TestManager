using Microsoft.AspNetCore.Mvc.Controllers;

namespace eHub.Scripting.Controllers;

/// <summary>This component loads and inserts ApiControllers from script files
/// into the ASP.NET pipeline.<br/>
/// Scripts will be automatically loaded at launch and hot reloaded on file changes.
/// Paths to script files can be managed from the config.</summary>
public class ControllerManager : IHostedService
{
    private readonly DynamicActionProvider _actionProvider;

    public ControllerManager(
        ScriptControllerProvider scriptControllerProvider,
        DynamicActionProvider actionProvider)
    {
        scriptControllerProvider.ControllersAdded += ScriptControllerProvider_ControllersAdded;
        scriptControllerProvider.ControllersRemoved += ScriptControllerProvider_ControllersRemoved;
        _actionProvider = actionProvider;
    }

    private void ScriptControllerProvider_ControllersAdded(object? sender, IReadOnlyCollection<ControllerActionDescriptor> e)
    {
        _actionProvider.AddControllers(e);
    }

    private void ScriptControllerProvider_ControllersRemoved(object? sender, IReadOnlyCollection<ControllerActionDescriptor> e)
    {
        _actionProvider.RemoveController(e);
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
