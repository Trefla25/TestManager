using System.Threading;
using System.Threading.Tasks;
using eHub.PlugIn;
using eMessenger;
using Microsoft.Extensions.Logging;

namespace eHub.Playground.Scripts2;

public class UpdateStatusConnector : IConnector
{
    private readonly IScopedMessenger _messenger;
    private readonly ILogger<MessengerStuff> _logger;

    public event UpdateStatusDelegate? UpdateStatus;

    public UpdateStatusConnector(IScopedMessenger messenger, ILogger<MessengerStuff> logger)
    {
        _messenger = messenger;
        _logger = logger;
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        var counterOne = 0;
        var charCounter = 40;

        while (!cancellationToken.IsCancellationRequested)
        {
            UpdateStatus?.Invoke("Changing value", "yeee");
            UpdateStatus?.Invoke("Amount of threads", counterOne++.ToString());
            await Task.Delay(250);
            UpdateStatus?.Invoke("Changing value", "no");
            UpdateStatus?.Invoke("Custom char", ((char)charCounter++).ToString());

            if (charCounter >= 100)
            {
                charCounter = 40;
            }
            UpdateStatus?.Invoke("Changing value", "looooooooooooooooooooooooooooooooooooooooooooooooooooooong");

            await Task.Delay(1000);
        }
    }
}
