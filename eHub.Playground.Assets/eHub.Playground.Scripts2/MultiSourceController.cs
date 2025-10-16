using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace eHub.Playground.Scripts2;

[ApiController]
[Route("api/multi")]
public class MultiSourceController : ControllerBase
{
    private readonly ILogger _logger;

    public MultiSourceController(ILogger<MultiSourceController> logger)
    {
        _logger = logger;
    }

    [HttpPost("dummy")]
    public async Task<IActionResult> PostDummy(DummyData dummy)
    {
        return Ok(dummy);
    }

    [HttpGet("ex")]
    public async Task<IActionResult> GetException()
    {
        throw new Exception("asdf");
    }

    [HttpGet("echo/{text}")]
    public IActionResult Echo(string text)
    {
        return Ok("1234567");
    }
}

public class DummyData
{
    public required string Name { get; set; }
    public string? Description { get; set; }
}
