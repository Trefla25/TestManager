using System.Net.Sockets;
using System.Net;

namespace eHub.Tests.Helper;

internal static class NetworkHelper
{
    /// <summary>
    /// Finds an available network port on localhost.
    /// This method starts a temporary TCP listener on port 0,
    /// which tells the OS to assign an unused port automatically.
    /// The assigned port is then retrieved and returned before the listener is stopped.
    /// </summary>
    /// <returns>A random, unused port number.</returns>
    public static int GetRandomUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
