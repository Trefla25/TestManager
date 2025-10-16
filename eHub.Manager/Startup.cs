using eController.WebUI.PlugIn;
using eController.WebUI.Utility.WebLoader;

namespace eHub.Manager;

public class Startup : ServerExtensionService
{
    public Startup(IConfiguration config, IServiceCollection services)
    {
    }

    public override async Task ConfigureScope(IServiceProvider services)
    {
        var service = services.GetRequiredService<IJsLoadService<Startup>>();
        await service.LoadJsAsync("ehub_helper.js");
    }
}
