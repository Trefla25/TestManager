using eScheduler.Library.Api;
using Microsoft.Extensions.Primitives;

namespace eHub.Config.Providers;

public class SchedulerApiConfigurationProvider : ConfigurationProvider 
{
    private readonly Uri _eHubUrl;
    private readonly IConfigurationRoot _configuration;
    private readonly Dictionary<string, string?> _defaultSettings;

    public SchedulerApiConfigurationProvider(IConfigurationRoot configuration)
    {
        _configuration = configuration;
        _defaultSettings = [];

        var endpointsSection = _configuration.GetSection("Kestrel:Endpoints");
        var kestrelUrl = endpointsSection.GetChildren().SelectNotNull(e => e?["Url"]).FirstOrDefault() ?? "http://localhost:5000";

        _eHubUrl = new UriBuilder(kestrelUrl) { Host = "localhost" }.Uri;
        _defaultSettings.Add("SchedulerApi:Apps:eHub:Url", _eHubUrl.ToString());

        ChangeToken.OnChange(() => _configuration.GetReloadToken(), Load);
    }

    /// <summary>
    /// Initializes the provider with default data or loads any necessary initial configuration.
    /// </summary>
    public override void Load()
    {
        Data.Clear();
        var schedulerApiSection = _configuration.GetSection(SchedulerApiConfig.DefaultKey);
        foreach (var kvp in schedulerApiSection.AsEnumerable())
        {
            Data[kvp.Key] = kvp.Value;
        }

        foreach (var defaultSetting in _defaultSettings)
        {
            if (!Data.ContainsKey(defaultSetting.Key))
            {
                Data[defaultSetting.Key] = defaultSetting.Value;
            }
        }
    }

    /// <summary>
    /// Adds or updates a configuration key-value pair.
    /// </summary>
    /// <param name="key">The configuration key.</param>
    /// <param name="value">The configuration value.</param>
    public override void Set(string key, string? value)
    {
        _defaultSettings[key] = value;
        Data[key] = value;

        OnReload();
    }

    /// <summary>
    /// Adds a connector API to the configuration.
    /// </summary>
    /// <param name="connectorName">The template name of the connector to add.</param>
    public void AddConnectorApi(string connectorName)
    {
        var url = new Uri(_eHubUrl, $"api/connectors/{connectorName}/scheduler");
        Set($"SchedulerApi:Apps:{connectorName}:Url", url.ToString());
    }

    /// <summary>
    /// Removes a configuration key.
    /// </summary>
    /// <param name="key">The key to remove.</param>
    /// <returns>True if the key was removed; otherwise, false.</returns>
    public bool Remove(string key)
    {
        _defaultSettings.Remove(key);

        if (Data.Remove(key))
        {
            OnReload(); 
            return true;
        }
        return false;
    }

    /// <summary>
    /// Removes a connector API from the configuration.
    /// </summary>
    /// <param name="connectorName">The template name of the connector to remove.</param>/// </param>
    /// <returns>True if the key was removed; otherwise, false.</returns>
    public bool RemoveConnectorApi(string connectorName)
    {
        return Remove($"SchedulerApi:Apps:{connectorName}:Url");
    }
}
