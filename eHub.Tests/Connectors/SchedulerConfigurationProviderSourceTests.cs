using Microsoft.Extensions.Configuration;
using eHub.Config.Providers;
using NSubstitute;
using FluentAssertions;

namespace eHub.Tests.Connectors;

[TestClass]
public class SchedulerConfigurationProviderSourceTests
{
    [TestMethod]
    public void Build_WhenCalled_CreatesProvider()
    {
        var configRoot = Substitute.For<IConfigurationRoot>();
        var source = new SchedulerApiConfigurationSource(configRoot);

        var builder = new ConfigurationBuilder();
        var provider = source.Build(builder);

        provider.Should().NotBeNull();
        provider.Should().BeOfType<SchedulerApiConfigurationProvider>();
    }

    [TestMethod]
    public void Build_WithNullConfiguration_ThrowsException()
    {
        IConfigurationRoot? nullConfig = null;
        var source = new SchedulerApiConfigurationSource(nullConfig!);

        Action act = () =>
        {
            var builder = new ConfigurationBuilder();
            source.Build(builder);
        };

        act.Should().Throw<NullReferenceException>();
    }

    [TestMethod]
    public void GetProvider_WhenBuilt_ReturnsSameInstance()
    {
        var configRoot = Substitute.For<IConfigurationRoot>();
        var source = new SchedulerApiConfigurationSource(configRoot);

        var builder = new ConfigurationBuilder();
        var builtProvider = source.Build(builder);
        var returnedProvider = source.GetProvider();

        returnedProvider.Should().BeSameAs(builtProvider);
    }

    [TestMethod]
    public void GetProvider_WhenNotBuilt_ThrowsException()
    {
        var configRoot = Substitute.For<IConfigurationRoot>();
        var source = new SchedulerApiConfigurationSource(configRoot);

        Action act = () =>
        {
            var provider = source.GetProvider();
            provider.Load();
        };

        act.Should().Throw<NullReferenceException>();
    }
}
