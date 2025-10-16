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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eHub.Tests.Connectors;

[TestClass]
public class PluginLoadTests
{
    public TestContext TestContext { get; set; } = default!;
    private string CaseTestPath { get; set; } = default!;
    public ServiceProvider ServiceProvider { get; }
    private ILoggerFactory DebugLoggerFactory { get; set; }
    private IPluginPublisher PluginPublisher { get; set; } = default!;
    private IEffortlessConfigurationRegistry MockConfigRegistry { get; set; }
    private PluginEngine PluginEngine { get; set; } = default!;
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

    [TestInitialize()]
    public void Startup()
    {
        var caseTestBasePath = Path.GetFullPath(Path.Join(TestContext.TestRunDirectory, Guid.NewGuid().ToString()));
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
                    Modules = new List<object>() {
                    new {
                        Module = "ImportFiles",
                        Init = true,
                        Scripts = CaseTestPath,
                        Config = CaseTestPath,
                        Watch = true },
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
    }

    [TestMethod]
    [Timeout(10_000)]
    public async Task TestConnectorEventListener()
    {
        // Setup
        var pluginPath = Path.GetFullPath(Path.Join(CaseTestPath, "File1.cs"));
        File.WriteAllText(pluginPath, """
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
            """);
        await PluginEngine.RunMain();

        var eventRecorder = new RecordingConnectorListener();
        var eventAwaiter = new AwaitableConnectorListener();

        // Act
        var connectionManager = new ConnectorManager(
            NullConfiguration.Instance,
            DebugLoggerFactory,
            Messenger,
            MessagingContext,
            PluginPublisher,
            ServiceProvider,
            [eventRecorder, eventAwaiter],
            MockConfigRegistry,
            null,
            Mocks.MeterFactory);
        await connectionManager.StartAsync(default);
        var result = await connectionManager.StartConnectorByType("PluginLoadTests.UnitTestConnector");
        Assert.IsTrue(result.IsOk);

        _ = await eventAwaiter.NextStatus();

        // Assert
        CollectionAssert.AreEquivalent(
            new KeyValuePair<string, string>[] { new("test_key", "test_value"), new("test_key2", "test_value2") },
            eventRecorder.Statuses.Last().statusDictionary.ToArray());
    }

    // https://github.com/ewms/eHub/issues/4
    [TestMethod]
    [Timeout(10_000)]
    public async Task TestChangeConnectorConfigShouldReload()
    {
        var pluginPath = Path.GetFullPath(Path.Join(CaseTestPath, "File1.cs"));
        File.WriteAllText(pluginPath, """
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
            public class UnitConf {
                public string Msg { get; set; }
            }
            """);
        var connectorJsonPath = Path.GetFullPath(Path.Join(CaseTestPath, "connector.json"));

        File.WriteAllText(connectorJsonPath, """
            {
                "Unit": {
                    "Type": "PluginLoadTests.UnitTestConnectorA",
                    "Config": { "Msg": "Default" }
                }
            }
            """);

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
            null,
            Mocks.MeterFactory);

        await connectionManager.StartAsync(default);

        // Assert before change
        await events.NextConfigReload();
        var (_, statusDictionary) = await events.NextStatus();
        Assert.AreEqual("Loaded A with Default", statusDictionary["status"]);


        events.ResetConfigReload();
        events.ResetStatus();
        // > Simulate config change
        File.WriteAllText(connectorJsonPath, """
            {
                "Unit": {
                    "Type": "PluginLoadTests.UnitTestConnectorA",
                    "Config": { "Msg": "Changed" }
                }
            }
            """);

        await events.NextConfigReload();
        (_, statusDictionary) = await events.NextStatus();
        Assert.AreEqual("Loaded A with Changed", statusDictionary["status"]);


        events.ResetConfigReload();
        events.ResetStatus();
        // > Simulate config change
        File.WriteAllText(connectorJsonPath, """
            {
                "Unit": {
                    "Type": "PluginLoadTests.UnitTestConnectorB",
                    "Config": { "Msg": "LoadB" }
                }
            }
            """);

        await events.NextConfigReload();
        (_, statusDictionary) = await events.NextStatus();
        Assert.AreEqual("Loaded B with LoadB", statusDictionary["status"]);
    }
}
