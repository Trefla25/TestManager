using eHub.PlugIn.Configuration;

namespace eHub.PlugIn;

/// <summary>
/// Implement this interface to configure a connector's default settings and validate its configuration.
/// </summary>
public interface IConnectorConfigurator : IConnector
{
    /// <summary>
    /// Configures the default settings for the connector using the provided configuration builder.
    /// </summary>
    /// <param name="builder">
    /// The configuration builder used to set up default values in a fluent manner.
    /// </param>
    static abstract void ConfigureDefaults(IConnectorConfigBuilder builder);    
}
