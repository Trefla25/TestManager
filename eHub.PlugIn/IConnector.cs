using Microsoft.Extensions.Options;

namespace eHub.PlugIn;

/// <summary>
/// Implement this interface in a public class to expose a connector.<br/>
/// A connector can be instantiated multiple times with difference run parameters via connector config files.
/// 
/// <para>
/// You can request any shared component via dependency injection to the constructor.<br/>
/// Common utilities like 
/// <see href="https://docs.microsoft.com/en-us/dotnet/core/extensions/logging?tabs=command-line#create-logs">Logging</see> and
/// <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-interfaces">Options</see>
/// are available.
/// </para>
/// <para>
/// Use <see cref="IOptions{TOptions}"/>, <see cref="IOptionsSnapshot{TOptions}"/>
/// or <see cref="IOptionsMonitor{TOptions}"/> with a custom object annotated with
/// <see cref="ScriptOptionsAttribute"/> to map the passed config.<br/>
/// You can refer to the 
/// <see href="https://docs.microsoft.com/en-us/aspnet/core/fundamentals/configuration/options?view=aspnetcore-6.0#options-interfaces">Options pattern in ASP.NET Core Documentation</see>
/// for usage.
/// </para>
/// </summary>
public interface IConnector
{
    /// <summary>
    /// Event to expose data in a key-value-store manner to the web UI about the state of the connector.<br/>
    /// Sent entries are managed by the IntegrationHub and can be removed by setting the value to <see langword="null" />
    /// </summary>
    public event UpdateStatusDelegate? UpdateStatus;

    /// <summary>The connector should start the work with this call.
    /// This call can and should enter a work loop, which only finishes when the connector gets stopped.
    /// </summary>
    /// <param name="cancellationToken">When the connector is requested to stop the token will be aborted.</param>
    /// <returns>A long running Task tracking the work state of the connector.</returns>
    Task Run(CancellationToken cancellationToken);
}

/// <summary>Delegate type to update status of Connectors</summary>
/// <param name="key">Any string to uniquely tag a value to set or update.</param>
/// <param name="value">The value to show for the tag. Or <see langword="null" /> to remove tag.</param>
public delegate void UpdateStatusDelegate(string key, string? value);
