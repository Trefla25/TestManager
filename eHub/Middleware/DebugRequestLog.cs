using System.Text;

namespace eHub.Middleware;

public class DebugRequestLog(ILogger<DebugRequestLog> logger, RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (logger.IsEnabled(LogLevel.Trace))
        {
            var request = context.Request;

            var strbLog = new StringBuilder();
            strbLog
                .Append(request.Method)
                .Append(' ')
                .Append(request.Path)
                .Append(' ')
                .Append(request.Protocol)
                .AppendLine();

            foreach (var header in request.Headers)
            {
                strbLog
                    .Append(header.Key)
                    .Append(": ")
                    .Append(header.Value)
                    .AppendLine();
            }

            logger.LogTrace($"Http Request\n{strbLog}");
        }
        await next(context);
    }
}
