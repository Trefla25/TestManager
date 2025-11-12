using eHub.Scripting;
using eHub.Scripting.Connectors;
using eController.Util.Configuration;
using eHub.Tests.Helper;
using ElementLogic.Configuration.Client;
using ElementLogic.Configuration.Configurations;
using eMessenger;
using eMessenger.Tests;
using ePlugin.Engine;
using ePlugin.Engine.Client;
using ePlugin.Engine.Config;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eHub.Tests.Connectors;

public class PluginLoadTests : IAsyncLifetime
{
    private string CaseTestPath { get; set; } = null!;
    public ServiceProvider ServiceProvider { get; }
    private ILoggerFactory DebugLoggerFactory { get; set; }
    private IPluginPublisher PluginPublisher { get; set; } = null!;
    private IEffortlessConfigurationRegistry MockConfigRegistry { get; set; }
    private PluginEngine PluginEngine { get; set; } = null!;
    private MessagingContext MessagingContext { get; set; } = new();
    private IMessenger Messenger { get; set; }

    public PluginLoadTests()
    {
        ServiceProvider = new ServiceCollection()
            .AddSingleton<IConfiguration>(new ConfigurationBuilder().Build())
            .AddLogging(builder => builder.AddDebug())
            .AddSingleton(TestingMessenger.CreateScoped())
            .AddSingleton<IMessenger>(x => x.GetRequiredService<IScopedMessenger>())
            .BuildServiceProvider();
        DebugLoggerFactory = ServiceProvider.GetRequiredService<ILoggerFactory>();
        Messenger = ServiceProvider.GetRequiredService<IMessenger>();
        MockConfigRegistry = Substitute.For<IEffortlessConfigurationRegistry>();
        MockConfigRegistry.Factory.Returns(new ConfigurationSourceFactory(ServiceProvider));
        MockConfigRegistry.AppConfigFolder.Returns(
            new DirectoryInfo(Path.Join(Environment.CurrentDirectory, "non_existent")));
    }

    public ValueTask InitializeAsync()
    {
        var caseTestBasePath = Path.GetFullPath(
            Path.Join(Path.GetTempPath(), "PluginLoadTests", Guid.NewGuid().ToString()));
        var dataPath = Path.Join(caseTestBasePath, "Plugins");
        CaseTestPath = Path.Join(caseTestBasePath, "In");

        try { Directory.Delete(CaseTestPath, true); } catch { }
        Directory.CreateDirectory(CaseTestPath);

        var mini = new PluginServiceProvider
        {
            DebugLoggerFactory,
            new PlengOptions()
            {
                AutoReload = false,
                DataPath = dataPath,
                Modules = ConfigurationUtil.GetConfigurationFromObject(new
                {
                    Modules = new List<object>()
                    {
                        new
                        {
                            Module = "ImportFiles",
                            Init = true,
                            Scripts = CaseTestPath,
                            Config = CaseTestPath,
                            Watch = true
                        },
                        new { Module = "CompileScripts" },
                        new { Module = "LoadAssemblies" },
                    }
                }).GetSection("Modules")
            }
        };
        PluginContract.EnsureContractAssemblyLoaded();
        mini.BuildPleng(NullConfiguration.Instance, DebugLoggerFactory);
        PluginPublisher = mini.GetRequiredService<IPluginPublisher>();
        PluginEngine = mini.GetRequiredService<PluginEngine>();

        return ValueTask.CompletedTask;
    }

    [Fact(Timeout = 10_000)]
    public async Task TestConnectorEventListener()
    {
        // Arrange
        var pluginPath = Path.GetFullPath(Path.Join(CaseTestPath, "File1.cs"));
        await File.WriteAllTextAsync(pluginPath, """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using eHub.PlugIn;
            namespace PluginLoadTests;
            public class UnitTestConnector : IConnector {
             public event UpdateStatusDelegate? UpdateStatus;
             public async Task Run(CancellationToken ct) {
                 UpdateStatus?.Invoke("test_key", "test_value");
                 UpdateStatus?.Invoke("test_key2", "test_value2");
             }
            }
            """, TestContext.Current.CancellationToken);
        
        await PluginEngine.RunMain();

        var eventRecorder = new RecordingConnectorListener();
        var eventAwaiter = new AwaitableConnectorListener();

        var connectionManager = new ConnectorManager(
            NullConfiguration.Instance,
            DebugLoggerFactory,
            Messenger,
            MessagingContext,
            PluginPublisher,
            ServiceProvider,
            [eventRecorder, eventAwaiter],
            MockConfigRegistry,
            null!,
            Mocks.MeterFactory);
        
        // Act
        await connectionManager.StartAsync(TestContext.Current.CancellationToken);
        var result = await connectionManager.StartConnectorByType("PluginLoadTests.UnitTestConnector");
        
        // Assert
        result.IsOk.Should().BeTrue();

        _ = await eventAwaiter.NextStatusAsync();

        // Assert
        eventRecorder.Statuses.Last().statusDictionary.ToArray().Should().BeEquivalentTo(
            new KeyValuePair<string, string>[] { new("test_key", "test_value"), new("test_key2", "test_value2") });
    }

    // https://github.com/ewms/eHub/issues/4
    [Fact(Timeout = 10_000)]
    public async Task TestChangeConnectorConfigShouldReload()
    {
        // Arrange
        var pluginPath = Path.GetFullPath(Path.Join(CaseTestPath, "File1.cs"));
        await File.WriteAllTextAsync(pluginPath, """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using eHub.PlugIn;
            using Microsoft.Extensions.Options;

            namespace PluginLoadTests;
            public abstract class BaseUnitTestConnector : IConnector {
             private readonly IOptionsMonitor<UnitConf> _options;
             private readonly string _name;
             private readonly IDisposable? confListener;
             public event UpdateStatusDelegate? UpdateStatus;

             public BaseUnitTestConnector(IOptionsMonitor<UnitConf> options, string name) {
                 _options = options;
                 _name = name;
                 confListener = options.OnChange(ConfigChanged);
             }

             public async Task Run(CancellationToken ct) {
                 ct.Register(() => { confListener?.Dispose(); });
                 ConfigChanged(_options.CurrentValue);
             }

             private void ConfigChanged(UnitConf confNew)
             {
                 UpdateStatus?.Invoke("status", "Loaded " + _name + " with " + confNew.Msg);
             }
            }

            public class UnitTestConnectorA : BaseUnitTestConnector {
             public UnitTestConnectorA(IOptionsMonitor<UnitConf> options) : base(options, "A") {}
            }

            public class UnitTestConnectorB : BaseUnitTestConnector {
             public UnitTestConnectorB(IOptionsMonitor<UnitConf> options) : base(options, "B") {}
            }

            [ScriptOptions]
            public class UnitConf  {
             public string Msg { get; set; }
            }
            """, TestContext.Current.CancellationToken);
        var connectorJsonPath = Path.GetFullPath(Path.Join(CaseTestPath, "connector.json"));

        await File.WriteAllTextAsync(connectorJsonPath, """
            {
                "Unit": {
                    "Type": "PluginLoadTests.UnitTestConnectorA",
                    "Config": { "Msg": "Default" }
                }
            }
            """, TestContext.Current.CancellationToken);

        await PluginEngine.RunMain();

        var events = new AwaitableConnectorListener();

        var connectionManager = new ConnectorManager(
            NullConfiguration.Instance,
            DebugLoggerFactory,
            Messenger,
            MessagingContext,
            PluginPublisher,
            ServiceProvider,
            [events],
            MockConfigRegistry,
            null!,
            Mocks.MeterFactory);

        // Act
        await connectionManager.StartAsync(TestContext.Current.CancellationToken);

        // Assert before change
        await events.NextConfigReloadAsync();
        var (_, statusDictionary) = await events.NextStatusAsync();
        statusDictionary["status"].Should().Be("Loaded A with Default");

        events.ResetConfigReload();
        events.ResetStatus();
        // > Simulate config change
        await File.WriteAllTextAsync(connectorJsonPath, """
            {
                "Unit": {
                    "Type": "PluginLoadTests.UnitTestConnectorA",
                    "Config": { "Msg": "Changed" }
                }
            }
            """, TestContext.Current.CancellationToken);

        await events.NextConfigReloadAsync();
        (_, statusDictionary) = await events.NextStatusAsync();
        statusDictionary["status"].Should().Be("Loaded A with Changed");


        events.ResetConfigReload();
        events.ResetStatus();
        // > Simulate config change
        await File.WriteAllTextAsync(connectorJsonPath, """
            {
                "Unit": {
                    "Type": "PluginLoadTests.UnitTestConnectorB",
                    "Config": { "Msg": "LoadB" }
                }
            }
            """, TestContext.Current.CancellationToken);

        await events.NextConfigReloadAsync();
        (_, statusDictionary) = await events.NextStatusAsync();
        statusDictionary["status"].Should().Be("Loaded B with LoadB");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}