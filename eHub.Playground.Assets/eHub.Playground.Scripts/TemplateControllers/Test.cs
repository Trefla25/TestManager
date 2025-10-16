using System.Text.Json;
using System.Xml.Serialization;
using Microsoft.AspNetCore.Mvc;

namespace eHub.Playground.Scripts.TemplateControllers;

[ApiController]
[Route("api/test")]
public class TestV2Controller : ControllerBase
{
    private readonly ILogger _logger;

    public TestV2Controller(ILogger<TestV2Controller> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> PostV1Data()
    {
        return Ok(new TestController.BinData());
    }

    [HttpPost("poster")]
    public async Task<IActionResult> PostV2Data()
    {
        return Ok("poster");
    }

    [HttpPost("info")]
    [Consumes("text/plain")]
    public async Task<IActionResult> GetId()
    {
        using var sr = new StreamReader(Request.Body);
        var txt = await sr.ReadToEndAsync();

        using var xmr = new StringReader(txt);
        var seri = new XmlSerializer(typeof(XData));
        var obj = seri.Deserialize(xmr);
        var xxx = JsonSerializer.Serialize(obj);

        return Ok(xxx);
    }
}

[XmlRoot("root")]
public class XData
{
    [XmlElement("elem")]
    public string Elem { get; set; }
}
