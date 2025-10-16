using System;
using System.Threading;
using System.Threading.Tasks;
using eHub.PlugIn;
using eHub.PlugIn.Configuration;
using eMessenger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace eHub.Playground.Scripts2;
public class MessengerStuff : IConnector, ISetupDependenciesConnector
{
    private readonly IMessenger _messenger;
    private readonly ILogger _logger;

    public event UpdateStatusDelegate? UpdateStatus;

    public MessengerStuff(IScopedMessenger messenger, ILogger<MessengerStuff> logger, CoolThing cool)
    {
        _messenger = messenger;
        _logger = logger;
        Console.WriteLine(cool.Name);
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        await _messenger.ListenAsync("ehub.dummy.listen", () =>
        {
            _logger.LogInformation("Gochu fam");
        });

        var counter = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            _messenger.Send("ehub.dummy.send", "Hello World!");
            UpdateStatus?.Invoke("Counter", counter++.ToString());
            UpdateStatus?.Invoke("API available?", "Yes");

            UpdateStatus?.Invoke("Nuke codes ready?", "Yes");


            await Task.Delay(100, cancellationToken);
        }
    }

    public static void SetupDependencies(ConnectorServicesBuilder builder)
    {
        builder.Services.AddSingleton(new CoolThing());
    }
}

public class CoolThing
{
    public string Name { get; set; } = "Cool Thing";
}
