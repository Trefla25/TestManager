using Microsoft.Extensions.Configuration;
using eHub.Config.Providers;
using NSubstitute;
using FluentAssertions;

namespace eHub.Tests.Connectors;

public class SchedulerConfigurationProviderSourceTests
{
    [Fact]
    public void Build_WhenCalled_CreatesProvider()
    {
        // Arrange
        var configRoot = Substitute.For<IConfigurationRoot>();
        var source = new SchedulerApiConfigurationSource(configRoot);

        // Act
        var builder = new ConfigurationBuilder();
        var provider = source.Build(builder);
        
        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<SchedulerApiConfigurationProvider>();
    }

    [Fact]
    public void Build_WithNullConfiguration_ThrowsException()
    {
        // Arrange
        IConfigurationRoot? nullConfig = null;
        var source = new SchedulerApiConfigurationSource(nullConfig!);
        
        // Act
        var act = () =>
        {
            var builder = new ConfigurationBuilder();
            source.Build(builder);
        };
        
        // Assert
        act.Should().Throw<NullReferenceException>();
    }

    [Fact]
    public void GetProvider_WhenBuilt_ReturnsSameInstance()
    {
        // Arrange
        var configRoot = Substitute.For<IConfigurationRoot>();
        var source = new SchedulerApiConfigurationSource(configRoot);

        // Act
        var builder = new ConfigurationBuilder();
        var builtProvider = source.Build(builder);
        var returnedProvider = source.GetProvider();
        
        // Assert
        returnedProvider.Should().BeSameAs(builtProvider);
    }

    [Fact]
    public void GetProvider_WhenNotBuilt_ThrowsException()
    {
        // Arrange
        var configRoot = Substitute.For<IConfigurationRoot>();
        var source = new SchedulerApiConfigurationSource(configRoot);
        
        // Act
        var act = () =>
        {
            var provider = source.GetProvider();
            provider.Load();
        };
        
        // Assert
        act.Should().Throw<NullReferenceException>();
    }
}
