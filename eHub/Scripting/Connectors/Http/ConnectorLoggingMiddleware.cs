namespace eHub.Scripting.Connectors.Http;

public class ConnectorLoggingMiddleware(
    RequestDelegate next,
    ILoggerFactory loggerFactory,
    string loggerName)
{
    private readonly RequestDelegate _next = next;
    private readonly ILogger _logger = loggerFactory.CreateLogger(loggerName);

    public async Task InvokeAsync(HttpContext context)
    {

        var originalResponseStream = context.Response.Body;
        await using var responseStream = new MemoryStream((int)(context.Response.ContentLength ?? 0));

        context.Response.Body = responseStream;

        try
        {
            await _next(context);

            LogHttpRequest(context);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            LogHttpRequest(context, ex);
        }
        finally
        {
            responseStream.Seek(0, SeekOrigin.Begin);
            await responseStream.CopyToAsync(originalResponseStream);
        }
    }

    private void LogHttpRequest(HttpContext context, Exception? exception = null)
    {
        var method = context.Request.Method;
        var path = context.Request.Path;
        var statusCode = context.Response.StatusCode;

        var logLevel = GetLogLevel(statusCode);

        if (logLevel != LogLevel.None)
        {
            if (exception != null)
            {
                _logger.Log(logLevel, 0, exception, "Http request failed with exception. Method: {method}, Path: {path}, StatusCode: {statusCode}", method, path, statusCode);
            }
            else
            {
                switch (logLevel)
                {
                    case LogLevel.Trace:
                    case LogLevel.Debug:
                    case LogLevel.Information:
                        _logger.Log(logLevel, 0, exception, "Http request Completed. Method: {method}, Path: {path}, StatusCode: {statusCode}", method, path, statusCode);
                        break;
                    case LogLevel.Warning:
                    case LogLevel.Error:
                    case LogLevel.Critical:
                        _logger.Log(logLevel, 0, null, "Http request failed. Method: {method}, Path: {path}, StatusCode: {statusCode}", context.Request.Method, context.Request.Path, context.Response.StatusCode);
                        break;
                }
            }

        }
    }

    private static LogLevel GetLogLevel(int statusCode)
    {
        if (statusCode == 500)
        {
            return LogLevel.Error;
        }
        else if (statusCode < 100 || (statusCode >= 400 && statusCode < 600))
        {
            return LogLevel.Warning;
        }
        else
        {
            return LogLevel.Information;
        }
    }
}
