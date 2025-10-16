using System.Text.Json;
using eHub.PlugIn;
using eController.Util.Serialization;
using Microsoft.Extensions.Options;
using static eHub.Playground.Scripts.TemplateConectors.TestConnector;

namespace eHub.Playground.Scripts.TemplateConectors;

public class TestConnector(
    ILogger<TestConnector> logger,
    IOptionsMonitor<ConnectorConf> conf
    ) : IConnector
{
    private readonly Guid _id = Guid.NewGuid();
    private bool _running = false;
    private IDisposable? _confListener;
    private bool _last_on = false;
    private bool _on = false;
    private int _alarmId;

    public event UpdateStatusDelegate? UpdateStatus;

    public void CheckWaitingEntry()
    {
    }

    public async Task Run(CancellationToken cancellationToken)
    {
        _running = true;
        _confListener = conf.OnChange(ConfigChanged);

        UpdateStatus?.Invoke(nameof(_running), _running.ToString());

        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                OnTimer(null);
            }
        }
        catch (OperationCanceledException) { }

        _running = false;
        _confListener?.Dispose();
    }

    private void OnTimer(object? state)
    {
        using var s1 = logger.BeginScope("sadfffgf {Fld}", 42);
        using var s2 = logger.BeginKvpScope(("kvps", "val45"));

        var text = JsonSerializer.Serialize(conf.CurrentValue, SharedJsonOptions.Default);

        _on = Random.Shared.Next(0, 2) == 0;
        bool? send = _on != _last_on ? _on : null;
        if (_on && _on != _last_on)
        {
            _alarmId = Random.Shared.Next();
        }
        _last_on = _on;

        logger.LogInformation("{Id} Sending {Text} {Status}", _id.GetHashCode(), text, send);

        UpdateStatus?.Invoke("last_send", DateTime.Now.ToString("hh:mm:ss"));
    }

    private void MqttSub_OnNewMessageReceived(byte[] payload)
    {
        UpdateStatus?.Invoke("last_rev", DateTime.Now.ToString("hh:mm:ss"));
        //logger.LogInformation("TestConnector got telegram {0}", Encoding.UTF8.GetString(e.ApplicationMessage.Payload));
        //HostTelegramReceivedEvent?.Invoke(this, new TelReceivedEventArgs() { Telegram = e.ApplicationMessage.Payload });
    }

    private void ConfigChanged(ConnectorConf confNew)
    {
        Console.WriteLine("CHANGED: {0}", confNew.Message);
    }

    [ScriptOptions]
    public class ConnectorConf
    {
        public string? Message { get; set; }

        public TimeSpan Timeout { get; set; }

        public string? MqttTopicXX { get; set; }
    }
}
