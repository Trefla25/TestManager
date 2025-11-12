using eHub.Scripting.Connectors.Configuration;
using eHub.Tests.Helper;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace eHub.Tests.Connectors.FluentApi;

public class KestrelBuilderTests
{
    private readonly KestrelBuilder _builder = new();

    [Fact]
    public void ConfigureServerLimits_WithValidLimits_AppliesLimits()
    {
        // Arrange
        _builder.ConfigureServerLimits(limits =>
        {
            limits.MaxConcurrentConnections = 100;
            limits.MaxRequestBodySize = 500000;
            limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
        });
        
        var kestrelOptions = new KestrelServerOptions();

        // Act
        _builder.ApplyLimits(kestrelOptions);
        
        // Assert
        kestrelOptions.Limits.MaxConcurrentConnections.Should().Be(100);
        kestrelOptions.Limits.MaxRequestBodySize.Should().Be(500000);
        kestrelOptions.Limits.KeepAliveTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task ApplyListeners_WithSingleListener_ConfiguresKestrelCorrectly()
    {
        // Arrange
        var port = NetworkHelper.GetRandomUnusedPort();
        _builder.Listen($"http://localhost:{port}");
        
        var webAppBuilder = WebApplication.CreateBuilder(new WebApplicationOptions());

        webAppBuilder.WebHost.UseKestrel(_builder.ApplyListeners);

        var app = webAppBuilder.Build();
        app.MapGet("/", () => "Test Response");

        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var client = new HttpClient();
        var response = await client.GetAsync($"http://localhost:{port}/", TestContext.Current.CancellationToken);
        
        // Assert
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        
        content.Should().Be("Test Response");

        // Cleanup
        await app.StopAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ApplyListeners_WithMultipleListeners_ConfiguresKestrelWithMultipleEndpoints()
    {
        // Arrange
        var portForAllInterfaces = NetworkHelper.GetRandomUnusedPort();
        var portForLocalhost = NetworkHelper.GetRandomUnusedPort();
        
        _builder.Listen($"http://0.0.0.0:{portForAllInterfaces}");
        _builder.Listen($"http://127.0.0.1:{portForLocalhost}");

        var webAppBuilder = WebApplication.CreateBuilder(new WebApplicationOptions());

        webAppBuilder.WebHost.UseKestrel(_builder.ApplyListeners);

        var app = webAppBuilder.Build();
        app.MapGet("/", () => "Test Response");

        await app.StartAsync(TestContext.Current.CancellationToken);

        // Act
        var client = new HttpClient();
        var responseFromAllInterfaces = await client.GetAsync($"http://localhost:{portForAllInterfaces}/", TestContext.Current.CancellationToken);
        
        // Assert
        responseFromAllInterfaces.EnsureSuccessStatusCode();
        var content1 = await responseFromAllInterfaces.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        content1.Should().Be("Test Response");

        var responseFromLocalhost = await client.GetAsync($"http://127.0.0.1:{portForLocalhost}/", TestContext.Current.CancellationToken);
        responseFromLocalhost.EnsureSuccessStatusCode();
        var content2 = await responseFromLocalhost.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        content2.Should().Be("Test Response");

        
        // Cleanup
        await app.StopAsync(TestContext.Current.CancellationToken);
    }
}
