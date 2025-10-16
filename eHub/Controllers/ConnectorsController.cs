using eHub.Contracts.Manager;
using eHub.Scripting.Connectors;
using Microsoft.AspNetCore.Mvc;

namespace eHub.Controllers;

[Route("api/hub/connectors")]
[ApiController]
public class ConnectorsController(ConnectorManager connectorManager, ILogger<ConnectorsController> logger) : ControllerBase
{
    [HttpGet("summary")]
    public ConnectorSummary GetAll()
    {
        return new(
            connectorManager.ActiveConnectors,
            connectorManager.AvailableConnectorTypes,
            connectorManager.AllConnectorTemplates
        );
    }


    [HttpPost("template/{name}/start")]
    public async Task<CreateResultDto> StartScript(string name) => new(await connectorManager.StartConnectorByTemplate(name, await TryCreateConfiguration(Request.Body)));

    [HttpPost("template/{name}/stop")]
    public async Task StopScript(string name) => await connectorManager.StopConnectorByName(name);

    [HttpPost("bytype/{type}/start")]
    public async Task<CreateResultDto> StartScriptByType(string type) => new(await connectorManager.StartConnectorByType(type, await TryCreateConfiguration(Request.Body)));

    private async Task<IConfiguration?> TryCreateConfiguration(Stream jsonStream)
    {
        try
        {
            using var mem = new MemoryStream();
            await jsonStream.CopyToAsync(mem);
            mem.Seek(0, SeekOrigin.Begin);
            var configBuilder = new ConfigurationBuilder();
            configBuilder.AddJsonStream(mem);
            return configBuilder.Build();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load config from api call");
            return null;
        }
    }

    /// <inheritdoc cref="ConnectorCreateResult"/>
    public record CreateResultDto(bool IsOk, string? Message)
    {
        public CreateResultDto(ConnectorCreateResult c) : this(c.IsOk, c.Message ?? c.Exception?.Message) { }
    }
}
