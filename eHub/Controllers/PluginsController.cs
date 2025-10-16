using ePlugin.Engine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eHub.Controllers;

[ApiController]
[Route("api/hub/plugins")]
public class PluginsController(PluginEngine pluginEngine)
{
    [HttpPost("reload")]
    [AllowAnonymous]
    public async Task Post()
    {
        await pluginEngine.RunMain();
    }
}
