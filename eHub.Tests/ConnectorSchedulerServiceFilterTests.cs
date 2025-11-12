using eHub.Controllers;
using eHub.Filters;
using eHub.Scripting.Connectors;
using eScheduler.Library.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace eHub.Tests;

public class ConnectorSchedulerServiceFilterTests
{
    private const string ConnectorName = "MyConnector";
    private readonly ISchedulerService _schedulerService;
    private readonly ConnectorSchedulerServiceFilter _serviceFilter;

    public class ConnectorSchedulerControllerSpy : ConnectorSchedulerController
    {
        public ISchedulerService SchedulerService => _schedulerService;
    }

    public ConnectorSchedulerServiceFilterTests()
    {
        var schedulerServiceProvider = Substitute.For<ISchedulerServiceProvider>();
        _schedulerService = Substitute.For<ISchedulerService>();
        schedulerServiceProvider.TryGetSchedulerService(ConnectorName, out Arg.Any<ISchedulerService?>())
            .Returns(call =>
            {
                call[1] = _schedulerService;
                return true;
            });

        _serviceFilter = new ConnectorSchedulerServiceFilter(
            schedulerServiceProvider,
            NullLogger<ConnectorSchedulerServiceFilter>.Instance);
    }

    [Fact]
    public void OnActionExecuting_WithValidConnectorName_SetsSchedulerService()
    {
        // Arrange
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller, ConnectorName);
        
        // Act
        _serviceFilter.OnActionExecuting(context);
        
        // Assert
        controller.SchedulerService.Should().BeSameAs(_schedulerService);
    }

    [Fact]
    public void OnActionExecuting_WithInvalidConnectorName_ReturnsBadRequest()
    {
        // Arrange
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller, "OtherConnector");
        
        // Act
        _serviceFilter.OnActionExecuting(context);
        
        // Assert
        context.Result.Should().BeOfType<BadRequestObjectResult>();
        controller.SchedulerService.Should().BeNull();

    }

    [Fact]
    public void OnActionExecuting_WithEmptyConnectorName_ReturnsBadRequest()
    {
        // Arrange
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller, "");
        
        // Act
        _serviceFilter.OnActionExecuting(context);
        
        // Assert
        context.Result.Should().BeOfType<BadRequestObjectResult>();
        controller.SchedulerService.Should().BeNull();
    }

    [Fact]
    public void OnActionExecuting_WithInvalidControllerType_ReturnServerError()
    {
        // Arrange
        var fakeController = new object();
        var context = CreateActionContext(fakeController, ConnectorName);
        
        // Act
        _serviceFilter.OnActionExecuting(context);
        
        // Assert
        context.Result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public void OnActionExecuting_WithMissingNameRouteParameter_DoesNothing()
    {
        // Arrange
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller);
        
        // Act
        _serviceFilter.OnActionExecuting(context);
        
        // Assert
        context.Result.Should().BeNull();
        controller.SchedulerService.Should().BeNull();
    }

    private static ActionExecutingContext CreateActionContext(object controller, string? connectorName = null)
    {
        var httpContext = new DefaultHttpContext();
        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        if (connectorName != null)
        {
            actionContext.RouteData.Values["name"] = connectorName;
        }

        return new ActionExecutingContext(
            actionContext,
            new List<IFilterMetadata>(),
            new Dictionary<string, object?>(),
            controller);
    }
}
