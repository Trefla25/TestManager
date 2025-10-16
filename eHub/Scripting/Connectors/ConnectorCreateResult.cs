namespace eHub.Scripting.Connectors;

/// <summary>Result of trying to start a connector.</summary>
/// <param name="IsOk">Whether the connector was started successfully.</param>
/// <param name="Message">General result message. Can be an error or information.</param>
/// <param name="Exception">The <see cref="System.Exception"/> if any occurred.</param>
public sealed record ConnectorCreateResult(bool IsOk, string? Message, Exception? Exception)
{
    private static readonly ConnectorCreateResult DefaultOk = new(true, null, null);
    internal static ConnectorCreateResult Ok() => DefaultOk;
    internal static ConnectorCreateResult Ok(string? message) => new(true, message, null);
    internal static ConnectorCreateResult Err(Exception? exception = null) => new(false, null, exception);
    internal static ConnectorCreateResult Err(string? message = null, Exception? exception = null) => new(false, message, exception);
    internal void Log(ILogger logger)
    {
        if (!IsOk)
        {
            logger.LogError(Exception, "{CreateMessage}", Message);
        }
    }
}
