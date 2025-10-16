using eHub.Contracts.Manager;
using eHub.Scripting.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace eHub.Controllers;

[Authorize]
[ApiController]
[Route("api/hub/controllers")]
public class ControllersController(
    IActionDescriptorCollectionProvider actionDescriptorCollectionProvider)
    : ControllerBase
{
    [HttpGet("summary")]
    [AllowAnonymous]
    public ActionResult<ListResult<RouteModel>> Get()
    {
        var routes = actionDescriptorCollectionProvider.ActionDescriptors.Items
            .Where(ad => ad.AttributeRouteInfo != null
                && ad.EndpointMetadata.OfType<DynamicActionAttribute>().Any())
            .Select(ad =>
            {
                var cad = ad as ControllerActionDescriptor;
                return new RouteModel(
                    ad.AttributeRouteInfo!.Template,
                    ad.EndpointMetadata.OfType<HttpMethodMetadata>().FirstOrDefault()?.HttpMethods ?? [],
                    ad.DisplayName,
                    cad?.ControllerName,
                    cad?.ActionName);
            })
            .ToList();

        return Ok(new ListResult<RouteModel>(routes));
    }

    public record ListResult<T>(IReadOnlyList<T> Items)
    {
        public bool Success => Items.Count > 0;
        public static readonly ListResult<T> Empty = new([]);
    }
}
