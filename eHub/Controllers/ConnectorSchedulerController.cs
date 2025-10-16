using eHub.Filters;
using eScheduler.Library.Abstractions;
using eScheduler.Library.Api;
using Microsoft.AspNetCore.Mvc;

namespace eHub.Controllers;

[Route("api/connectors/{name}/scheduler")]
[ServiceFilter(typeof(ConnectorSchedulerServiceFilter))]
public class ConnectorSchedulerController : SchedulerController
{
    public void SetSchedulerService(ISchedulerService schedulerService)
    {
        _schedulerService = schedulerService;
    }
}
