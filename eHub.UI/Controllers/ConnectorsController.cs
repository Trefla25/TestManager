using System.Collections.Immutable;
using System.Text.Json;
using eHub.Contracts;
using eHub.Contracts.UIConfig;
using eMessenger;
using Microsoft.AspNetCore.Mvc;

namespace eHub.UI.Controllers;

[ApiController]
[Route("api/connectors")]
public class ConnectorsController(IMessenger messenger) : ControllerBase
{
    [HttpGet("export")]
    public async Task<IActionResult> DownloadPacketsAsJson(
        [FromQuery] ConnectorIdentifier connectorIdentifier,
        [FromQuery] string filterJson,
        [FromQuery] string title)
    {
        var connectorIdentifiersString = connectorIdentifier.ToString().Split(";");

        List<PacketDto> packets = [];

        foreach (var connectorIdentifierString in connectorIdentifiersString)
        {
            var connectorIdentifierParts = connectorIdentifierString.Split(":");
            var newConnectorIdentifier = new ConnectorIdentifier(connectorIdentifierParts[0], connectorIdentifierParts[1]);

            var filter = JsonSerializer.Deserialize<PacketRequestDto>(filterJson)!;
            var resp = await messenger.AskAsync<PacketRequestDto, ConnectorPacketsExportDto>(
                ConnectorContract.PacketExportTopic(newConnectorIdentifier), filter)
                .FirstOrDefaultResponse();

            if (resp == null)
            {
                continue;
            }

            packets.AddRange(resp.Packets);
        }

        if (packets.Count == 0)
        {
            return NoContent();
        }

        var exportData = new ConnectorPacketsExportDto([.. packets]);

        byte[] byteArray = JsonSerializer.SerializeToUtf8Bytes(exportData);
        return File(byteArray, "application/json", $"{title}.json");
    }
}
