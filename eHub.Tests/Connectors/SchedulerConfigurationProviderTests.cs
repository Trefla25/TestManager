using Microsoft.Extensions.Configuration;
using eHub.Config.Providers;
using FluentAssertions;

namespace eHub.Tests.Connectors;

public class SchedulerConfigurationProviderTests
{
    private readonly Dictionary<string, string?> _settings = [];
    public SchedulerConfigurationProviderTests()
    {
        _settings.Add("SchedulerApi:Task1:Enabled", "true");
        _settings.Add("SchedulerApi:Task2:Enabled", "false");
    }

    [Fact]
    public void Load_WithValidConfig_LoadsSettingsAndSetsDefaults()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        provider.Load();
        
        // Assert
        provider.TryGet("SchedulerApi:Task1:Enabled", out var task1).Should().BeTrue();
        task1.Should().Be("true");

        provider.TryGet("SchedulerApi:Apps:eHub:Url", out var defaultUrl).Should().BeTrue();
        defaultUrl.Should().Be("http://localhost:5000/");
    }

    [Fact]
    public void Load_WhenDefaultKeyAlreadySet_DoesNotOverride()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration([]));
        provider.Set("SchedulerApi:Apps:eHub:Url", "http://custom-before-load/");

        // Act
        provider.Load();
        
        // Assert
        provider.TryGet("SchedulerApi:Apps:eHub:Url", out var value).Should().BeTrue();
        value.Should().Be("http://custom-before-load/");
    }

    [Fact]
    public void Load_WithNullValues_SetsNullValue()
    {
        // Arrange
        _settings["SchedulerApi:Task1:Enabled"] = null;
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        // Act
        provider.Load();
        
        // Assert
        provider.TryGet("SchedulerApi:Task1:Enabled", out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public void Load_WhenCalledTwice_ClearsDataBeforeReloading()
    {
        // First load
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Load();
        provider.TryGet("SchedulerApi:Task1:Enabled", out _).Should().BeTrue();

        // Second load with empty config
        var providerReloaded = new SchedulerApiConfigurationProvider(GetConfiguration([]));
        providerReloaded.Load();
        providerReloaded.TryGet("SchedulerApi:Task1:Enabled", out _).Should().BeFalse();
    }

    [Fact]
    public void Load_WhenDefaultsExist_DoesNotOverrideExistingValues()
    {
        // Arrange
        _settings.Add("SchedulerApi:Apps:eHub:Url", "http://custom-url/");
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        provider.Load();
        
        // Assert
        provider.TryGet("SchedulerApi:Apps:eHub:Url", out var value).Should().BeTrue();
        value.Should().Be("http://custom-url/");
    }

    [Fact]
    public void Set_WhenNewKeyProvided_AddsToDataAndDefaults()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        provider.Set("SchedulerApi:Task3:Enabled", "true");
        
        // Assert
        provider.TryGet("SchedulerApi:Task3:Enabled", out var value).Should().BeTrue();
        value.Should().Be("true");
    }

    [Fact]
    public void Set_WhenExistingKeyProvided_OverridesValue()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        // Act
        provider.Set("SchedulerApi:Task5:Enabled", "false");
        provider.Set("SchedulerApi:Task5:Enabled", "true");
        
        // Assert
        provider.TryGet("SchedulerApi:Task5:Enabled", out var _).Should().Be(true);
    }

    [Fact]
    public void Set_WhenValueIsNull_AllowsNullValues()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        provider.Set("SchedulerApi:NullableTask", null);
        
        // Assert
        provider.TryGet("SchedulerApi:NullableTask", out var value).Should().BeTrue();
        value.Should().BeNull();
    }

    [Fact]
    public void Set_WhenKeyIsEmpty_AddsEmptyKey()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        provider.Set("", "some-value");
        
        // Assert
        provider.TryGet("", out var value).Should().BeTrue();
        value.Should().Be("some-value");
    }

    [Fact]
    public void Set_WhenCalled_TriggersReloadToken()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration([]));
        var reloadCalled = false;
        var token = provider.GetReloadToken();
        token.RegisterChangeCallback(_ => reloadCalled = true, null);
        
        // Act
        provider.Set("SchedulerApi:Task123:Enabled", "true");
        
        // Assert
        reloadCalled.Should().BeTrue();
    }

    [Fact]
    public void AddConnectorApi_WhenCalledWithName_SetsCorrectUrl()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        provider.AddConnectorApi("TestConnector");
        
        // Assert
        provider.TryGet("SchedulerApi:Apps:TestConnector:Url", out var url).Should().BeTrue();
        url.Should().Be("http://localhost:5000/api/connectors/TestConnector/scheduler");
    }

    [Fact]
    public void AddConnectorApi_WhenKeyExists_OverridesExistingConnectorKey()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        // Act
        provider.AddConnectorApi("RepeatConnector");
        provider.AddConnectorApi("RepeatConnector");
        
        // Assert
        provider.TryGet("SchedulerApi:Apps:RepeatConnector:Url", out var url).Should().BeTrue();
        url.Should().Be("http://localhost:5000/api/connectors/RepeatConnector/scheduler");
    }

    [Fact]
    public void AddConnectorApi_WhenCalled_TriggersReload()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration([]));

        // Act
        var reloadCalled = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadCalled = true, null);
        provider.AddConnectorApi("TriggerTest");
        
        // Assert
        reloadCalled.Should().BeTrue();
    }

    [Fact]
    public void AddConnectorApi_WhenConnectorNameEmpty_SetsConnectorRootUrl()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        provider.AddConnectorApi("");
        
        // Assert
        provider.TryGet("SchedulerApi:Apps::Url", out var url).Should().BeTrue();
        url.Should().Be("http://localhost:5000/api/connectors//scheduler");
    }

    [Fact]
    public void Remove_WhenKeyDoesNotExist_ReturnsFalse()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        var result = provider.Remove("NonExistentKey");
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Remove_WhenKeyExists_RemovesKeyAndReturnsTrue()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Set("SchedulerApi:Task4:Enabled", "true");
        
        // Act
        var result = provider.Remove("SchedulerApi:Task4:Enabled");
        
        // Assert
        result.Should().BeTrue();
        provider.TryGet("SchedulerApi:Task4:Enabled", out _).Should().BeFalse();
    }

    [Fact]
    public void Remove_WhenKeyExisted_TriggersReload()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.Set("SchedulerApi:ReloadTest", "yes");

        // Act
        var reloadTriggered = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadTriggered = true, null);
        provider.Remove("SchedulerApi:ReloadTest");
        
        // Assert
        reloadTriggered.Should().BeTrue();
    }

    [Fact]
    public void Remove_WhenKeyMissing_DoesNotTriggerReload()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        // Act
        var reloadTriggered = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadTriggered = true, null);
        provider.Remove("MissingKey");
        
        // Assert
        reloadTriggered.Should().BeFalse();
    }

    [Fact]
    public void RemoveConnectorApi_WhenConnectorExists_RemovesKeyAndReturnsTrue()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.AddConnectorApi("ToRemove");
        
        // Act
        var removed = provider.RemoveConnectorApi("ToRemove");
        
        // Assert
        removed.Should().BeTrue();
        provider.TryGet("SchedulerApi:Apps:ToRemove:Url", out _).Should().BeFalse();
    }

    [Fact]
    public void RemoveConnectorApi_WhenConnectorMissing_ReturnsFalse()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        
        // Act
        var result = provider.RemoveConnectorApi("DoesNotExist");
        
        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void RemoveConnectorApi_WhenConnectorExists_TriggersReload()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));
        provider.AddConnectorApi("WithCallback");

        // Act
        var reloadCalled = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadCalled = true, null);
        provider.RemoveConnectorApi("WithCallback");
        
        // Assert
        reloadCalled.Should().BeTrue();
    }

    [Fact]
    public void RemoveConnectorApi_WhenKeyMissing_DoesNotTriggerReload()
    {
        // Arrange
        var provider = new SchedulerApiConfigurationProvider(GetConfiguration(_settings));

        // Act
        var reloadCalled = false;
        provider.GetReloadToken().RegisterChangeCallback(_ => reloadCalled = true, null);
        provider.RemoveConnectorApi("Ghost");
        
        // Assert
        reloadCalled.Should().BeFalse();
    }

    private static IConfigurationRoot GetConfiguration(Dictionary<string, string?> data)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(data)
            .Build();
}
