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

[TestClass]
public class ConnectorSchedulerServiceFilterTests
{
    private const string ConnectorName = "MyConnector";
    private ISchedulerServiceProvider _schedulerServiceProvider = null!;
    private ISchedulerService _schedulerService = null!;
    private ConnectorSchedulerServiceFilter _serviceFilter = null!;

    public class ConnectorSchedulerControllerSpy : ConnectorSchedulerController
    {
        public ISchedulerService SchedulerService => _schedulerService;
    }

    [TestInitialize]
    public void TestInitialize()
    {
        _schedulerServiceProvider = Substitute.For<ISchedulerServiceProvider>();
        _schedulerService = Substitute.For<ISchedulerService>();
        _schedulerServiceProvider.TryGetSchedulerService(ConnectorName, out Arg.Any<ISchedulerService?>())
            .Returns(call =>
            {
                call[1] = _schedulerService;
                return true;
            });

        _serviceFilter = new ConnectorSchedulerServiceFilter(
            _schedulerServiceProvider,
            NullLogger<ConnectorSchedulerServiceFilter>.Instance);
    }

    [TestMethod]
    public void OnActionExecuting_WithValidConnectorName_SetsSchedulerService()
    {
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller, ConnectorName);

        _serviceFilter.OnActionExecuting(context);

        controller.SchedulerService.Should().BeSameAs(_schedulerService);
    }

    [TestMethod]
    public void OnActionExecuting_WithInvalidConnectorName_ReturnsBadRequest()
    {
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller, "OtherConnector");

        _serviceFilter.OnActionExecuting(context);

        context.Result.Should().BeOfType<BadRequestObjectResult>();
        controller.SchedulerService.Should().BeNull();

    }

    [TestMethod]
    public void OnActionExecuting_WithEmptyConnectorName_ReturnsBadRequest()
    {
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller, "");

        _serviceFilter.OnActionExecuting(context);

        context.Result.Should().BeOfType<BadRequestObjectResult>();
        controller.SchedulerService.Should().BeNull();
    }

    [TestMethod]
    public void OnActionExecuting_WithInvalidControllerType_ReturnServerError()
    {
        var fakeController = new object();
        var context = CreateActionContext(fakeController, ConnectorName);

        _serviceFilter.OnActionExecuting(context);

        context.Result.Should().BeOfType<StatusCodeResult>().Which.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [TestMethod]
    public void OnActionExecuting_WithMissingNameRouteParameter_DoesNothing()
    {
        var controller = new ConnectorSchedulerControllerSpy();
        var context = CreateActionContext(controller);

        _serviceFilter.OnActionExecuting(context);

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

