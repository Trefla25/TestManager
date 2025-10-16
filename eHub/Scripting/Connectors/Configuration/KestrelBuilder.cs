using System.Net;
using eHub.PlugIn.Configuration;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace eHub.Scripting.Connectors.Configuration;

public class KestrelBuilder : IKestrelBuilder
{
    private readonly List<(IPEndPoint EndPoint, Action<ListenOptions>? Configure)> _listeners = [];
    private Action<KestrelServerLimits>? _configureLimits;

    public IKestrelBuilder ConfigureServerLimits(Action<KestrelServerLimits> configure)
    {
        _configureLimits = configure;
        return this;
    }

    public IKestrelBuilder Listen(string url, Action<ListenOptions>? configure = null)
    {
        var uri = new Uri(url);
        int port = uri.Port;

        var ipAddress = uri.Host switch
        {
            "localhost" => IPAddress.Loopback,
            _ => IPAddress.Parse(uri.Host)
        };

        _listeners.Add((new IPEndPoint(ipAddress, port), configure));

        return this;
    }

    public void ApplyLimits(KestrelServerOptions kestrelOptions)
    {
        _configureLimits?.Invoke(kestrelOptions.Limits);
    }

    public void ApplyListeners(KestrelServerOptions kestrelOptions)
    {
        foreach (var (endpoint, configure) in _listeners)
        {
            kestrelOptions.Listen(endpoint, configure ?? (_ => { }));
        }
    }
}
