using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using eHub.PlugIn;

namespace eHub.Playground.ScriptsHttpConnector;

[ScriptOptions]
public class HttpConnectorConfig
{
	public string? UrlEndpoint { get; set; }
}
