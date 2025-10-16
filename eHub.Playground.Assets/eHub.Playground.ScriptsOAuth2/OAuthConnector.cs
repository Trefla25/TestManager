using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using eHub.PlugIn;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace eHub.Playground.ScriptsOAuth2;
public class OAuthConnector(
	IOptions<Config> options,
	ILogger<OAuthConnector> logger,
	IHttpClientFactory httpClientFactory) : IConnector
{
	private readonly Config _config = options.Value;

	public event UpdateStatusDelegate? UpdateStatus;

	public async Task Run(CancellationToken cancellationToken)
	{
		try
		{
			using (var httpClient = httpClientFactory.CreateClient("BearerToken"))
			{
				var response = await httpClient.GetAsync(_config.UrlEndpoint, cancellationToken);

				if (response.IsSuccessStatusCode)
				{
					var obj = await response.Content.ReadFromJsonAsync<object>(cancellationToken);
				}
			}

			using (var httpClient = httpClientFactory.CreateClient("Basic"))
			{
				var response = await httpClient.GetAsync(_config.UrlEndpoint, cancellationToken);

				if (response.IsSuccessStatusCode)
				{
					var obj = await response.Content.ReadFromJsonAsync<object>(cancellationToken);
				}
			}
		}
		catch(Exception ex)
		{
			logger.LogError(ex, "http request failed");
		}
	}
}
