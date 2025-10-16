using Microsoft.Extensions.Configuration;
using eHub.Config.Providers;
using FluentAssertions;

namespace eHub.Tests.Connectors;

[TestClass]
public class SchedulerConfigurationProviderTests
{
    private readonly Dictionary<string, string?> _settings = [];

    [TestInitialize]
    public void TestInitialize()
    {
        _settings.Add("SchedulerApi:Task1:Enabled", "true");
        _settings.Add("SchedulerApi:Task2:Enabled", "false");
    }

    [TestMethod]
    public void Load_WithValidConfig_LoadsSettingsAndSetsDefaults()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Load();

        provider.TryGet("SchedulerApi:Task1:Enabled", out var task1).Should().BeTrue();
        task1.Should().Be("true");

        provider.TryGet("SchedulerApi:Apps:eHub:Url", out var defaultUrl).Should().BeTrue();
        defaultUrl.Should().Be("http://localhost:5000/");
    }

    [TestMethod]
    public void Load_WhenDefaultKeyAlreadySet_DoesNotOverride()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration([]));

        provider.Set("SchedulerApi:Apps:eHub:Url", "http://custom-before-load/");
        provider.Load();

        provider.TryGet("SchedulerApi:Apps:eHub:Url", out var value).Should().BeTrue();
        value.Should().Be("http://custom-before-load/");
    }

    [TestMethod]
    public void Load_WithNullValues_SetsNullValue()
    {
        _settings["SchedulerApi:Task1:Enabled"] = null;

        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Load();

        provider.TryGet("SchedulerApi:Task1:Enabled", out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [TestMethod]
    public void Load_WhenCalledTwice_ClearsDataBeforeReloading()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Load();

        provider.TryGet("SchedulerApi:Task1:Enabled", out _).Should().BeTrue();

        var providerReloaded = new SchedulerApiConfigurationProvider(GetConfiguration([]));
        providerReloaded.Load();

        providerReloaded.TryGet("SchedulerApi:Task1:Enabled", out _).Should().BeFalse();
    }

    [TestMethod]
    public void Load_WhenDefaultsExist_DoesNotOverrideExistingValues()
    {
        _settings.Add("SchedulerApi:Apps:eHub:Url", "http://custom-url/");
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        provider.Load();

        provider.TryGet("SchedulerApi:Apps:eHub:Url", out var value).Should().BeTrue();
        value.Should().Be("http://custom-url/");
    }

    [TestMethod]
    public void Set_WhenNewKeyProvided_AddsToDataAndDefaults()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Set("SchedulerApi:Task3:Enabled", "true");

        provider.TryGet("SchedulerApi:Task3:Enabled", out var value).Should().BeTrue();
        value.Should().Be("true");
    }

    [TestMethod]
    public void Set_WhenExistingKeyProvided_OverridesValue()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        provider.Set("SchedulerApi:Task5:Enabled", "false");
        provider.Set("SchedulerApi:Task5:Enabled", "true");

        provider.TryGet("SchedulerApi:Task5:Enabled", out var value).Should().Be(true);
    }

    [TestMethod]
    public void Set_WhenValueIsNull_AllowsNullValues()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        provider.Set("SchedulerApi:NullableTask", null);

        provider.TryGet("SchedulerApi:NullableTask", out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [TestMethod]
    public void Set_WhenKeyIsEmpty_AddsEmptyKey()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        provider.Set("", "some-value");

        provider.TryGet("", out var value).Should().BeTrue();
        value.Should().Be("some-value");
    }

    [TestMethod]
    public void Set_WhenCalled_TriggersReloadToken()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration([]));

        bool reloadCalled = false;
        var token = provider.GetReloadToken();

        token.RegisterChangeCallback(_ => reloadCalled = true, null);

        provider.Set("SchedulerApi:Task123:Enabled", "true");

        reloadCalled.Should().BeTrue();
    }

    [TestMethod]
    public void AddConnectorApi_WhenCalledWithName_SetsCorrectUrl()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.AddConnectorApi("TestConnector");

        provider.TryGet("SchedulerApi:Apps:TestConnector:Url", out var url).Should().BeTrue();
        url.Should().Be("http://localhost:5000/api/connectors/TestConnector/scheduler");
    }

    [TestMethod]
    public void AddConnectorApi_WhenKeyExists_OverridesExistingConnectorKey()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        provider.AddConnectorApi("RepeatConnector");
        provider.AddConnectorApi("RepeatConnector");

        provider.TryGet("SchedulerApi:Apps:RepeatConnector:Url", out var url).Should().BeTrue();
        url.Should().Be("http://localhost:5000/api/connectors/RepeatConnector/scheduler");
    }

    [TestMethod]
    public void AddConnectorApi_WhenCalled_TriggersReload()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration([]));

        bool reloadCalled = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadCalled = true, null);

        provider.AddConnectorApi("TriggerTest");

        reloadCalled.Should().BeTrue();
    }

    [TestMethod]
    public void AddConnectorApi_WhenConnectorNameEmpty_SetsConnectorRootUrl()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.AddConnectorApi("");

        provider.TryGet("SchedulerApi:Apps::Url", out var url).Should().BeTrue();
        url.Should().Be("http://localhost:5000/api/connectors//scheduler");
    }

    [TestMethod]
    public void Remove_WhenKeyDoesNotExist_ReturnsFalse()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        var result = provider.Remove("NonExistentKey");

        result.Should().BeFalse();
    }

    [TestMethod]
    public void Remove_WhenKeyExists_RemovesKeyAndReturnsTrue()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Set("SchedulerApi:Task4:Enabled", "true");

        var result = provider.Remove("SchedulerApi:Task4:Enabled");
        result.Should().BeTrue();

        provider.TryGet("SchedulerApi:Task4:Enabled", out _).Should().BeFalse();
    }

    [TestMethod]
    public void Remove_WhenKeyExisted_TriggersReload()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Set("SchedulerApi:ReloadTest", "yes");

        bool reloadTriggered = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadTriggered = true, null);

        provider.Remove("SchedulerApi:ReloadTest");

        reloadTriggered.Should().BeTrue();
    }

    [TestMethod]
    public void Remove_WhenKeyMissing_DoesNotTriggerReload()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        bool reloadTriggered = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadTriggered = true, null);

        provider.Remove("MissingKey");

        reloadTriggered.Should().BeFalse();
    }

    [TestMethod]
    public void RemoveConnectorApi_WhenConnectorExists_RemovesKeyAndReturnsTrue()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.AddConnectorApi("ToRemove");

        var removed = provider.RemoveConnectorApi("ToRemove");

        removed.Should().BeTrue();
        provider.TryGet("SchedulerApi:Apps:ToRemove:Url", out _).Should().BeFalse();
    }

    [TestMethod]
    public void RemoveConnectorApi_WhenConnectorMissing_ReturnsFalse()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        var result = provider.RemoveConnectorApi("DoesNotExist");

        result.Should().BeFalse();
    }

    [TestMethod]
    public void RemoveConnectorApi_WhenConnectorExists_TriggersReload()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.AddConnectorApi("WithCallback");

        bool reloadCalled = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadCalled = true, null);

        provider.RemoveConnectorApi("WithCallback");

        reloadCalled.Should().BeTrue();
    }

    [TestMethod]
    public void RemoveConnectorApi_WhenKeyMissing_DoesNotTriggerReload()
    {
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        bool reloadCalled = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadCalled = true, null);

        provider.RemoveConnectorApi("Ghost");

        reloadCalled.Should().BeFalse();
    }

    private static IConfigurationRoot GetConfiguration(Dictionary<string, string?> data)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
}
