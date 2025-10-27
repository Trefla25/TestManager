using eHub.PlugIn;
using eHub.PlugIn.Configuration;
using eHub.Scripting.Connectors.Features;
using eController.Util.Configuration;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace eHub.Tests.Connectors.DependencySetup;

[TestClass]
public class DependencySetupTests
{
    [TestMethod]
    public void
        GivenConnectorImplementsISetupDependenciesConnector_WhenSetupDependenciesCalled_ThenDependenciesAreAvailable()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = NullConfiguration.Instance;

        // Act
        DependencyInjectionFeature.InvokeDependencySetup(typeof(TestConnector), services, configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var dependency = serviceProvider.GetService<TestDependency>();
        dependency.Should().NotBeNull();
        dependency.Value.Should().Be(42);
    }

    [TestMethod]
    public void
        GivenInheritedConnectorWhereBaseImplementsISetupDependenciesConnector_WhenSetupDependenciesCalled_ThenDependenciesAreAvailable()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = NullConfiguration.Instance;

        // Act
        DependencyInjectionFeature.InvokeDependencySetup(typeof(InheritedTestConnectorWithoutRedeclaration), services,
            configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var dependency = serviceProvider.GetService<TestDependency>();
        dependency.Should().NotBeNull();
        dependency.Value.Should().Be(42);
    }

    [TestMethod]
    public void
        GivenInheritedConnectorWhereConnectorReImplementsISetupDependenciesConnector_WhenSetupDependenciesCalled_ThenDependenciesAreAvailable()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = NullConfiguration.Instance;

        // Act
        DependencyInjectionFeature.InvokeDependencySetup(typeof(InheritedTestConnectorWithRedeclaration), services,
            configuration);

        // Assert
        var serviceProvider = services.BuildServiceProvider();
        var dependency = serviceProvider.GetService<TestDependency>();
        dependency.Should().NotBeNull();
        dependency.Value.Should().Be(100);
    }

    private class TestConnector : ISetupDependenciesConnector
    {
        public event UpdateStatusDelegate? UpdateStatus;
        public Task Run(CancellationToken cancellationToken) => Task.CompletedTask;

        public static void SetupDependencies(ConnectorServicesBuilder builder)
        {
            builder.Services.AddSingleton(new TestDependency(42));
        }
    }

    private class InheritedTestConnectorWithoutRedeclaration : TestConnector
    {
    }

    private class InheritedTestConnectorWithRedeclaration : TestConnector, ISetupDependenciesConnector
    {
        public new static void SetupDependencies(ConnectorServicesBuilder builder)
        {
            builder.Services.AddSingleton(new TestDependency(100));
        }
    }

    private record TestDependency(int Value);
}
