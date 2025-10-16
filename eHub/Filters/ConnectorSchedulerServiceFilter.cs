using eHub.Controllers;
using eHub.Scripting.Connectors;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace eHub.Filters;

public class ConnectorSchedulerServiceFilter(
    ISchedulerServiceProvider schedulerServiceProvider,
    ILogger<ConnectorSchedulerServiceFilter> logger)
    : IActionFilter
{
    private readonly ISchedulerServiceProvider _schedulerServiceProvider = schedulerServiceProvider;
    private readonly ILogger<ConnectorSchedulerServiceFilter> _logger = logger;

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.RouteData.Values.TryGetValue("name", out var connectorNameObj) || connectorNameObj is not string connectorName)
        {
            return;
        }

        if (!_schedulerServiceProvider.TryGetSchedulerService(connectorName, out var schedulerService))
        {
            _logger.LogError("Could not get scheduler service for connector {connectorName}!", connectorName);
            context.Result = new BadRequestObjectResult($"Could not get scheduler service for connector {connectorName}!");
            return;
        }

        // Set the scheduler service to be accessible by the controller
        if (context.Controller is not ConnectorSchedulerController controller)
        {
            _logger.LogError("The {actionFilter} context controller is not of type {controller}!", nameof(ConnectorSchedulerServiceFilter), nameof(ConnectorSchedulerController));
            context.Result = new StatusCodeResult(StatusCodes.Status500InternalServerError);
            return;
        }

        controller.SetSchedulerService(schedulerService);
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
