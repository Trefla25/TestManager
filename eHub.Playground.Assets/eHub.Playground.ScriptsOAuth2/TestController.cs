using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace eHub.Playground.ScriptsOAuth2;

[Authorize]
[ApiController]
[Route("api")]
public class TestController : ControllerBase
{
	[HttpGet("test")]
	public IActionResult TestGet()
	{
		return Ok("OK");
	}
}
