using System.Net;
using System.Net.Sockets;
using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class KestrelBuilderTests
{
    private readonly KestrelBuilder _builder = new();

    [TestMethod]
    public void ConfigureServerLimits_WithValidLimits_AppliesLimits()
    {
        _builder.ConfigureServerLimits(limits =>
        {
            limits.MaxConcurrentConnections = 100;
            limits.MaxRequestBodySize = 500000;
            limits.KeepAliveTimeout = TimeSpan.FromSeconds(30);
        });

        var kestrelOptions = new KestrelServerOptions();
        _builder.ApplyLimits(kestrelOptions);

        kestrelOptions.Limits.MaxConcurrentConnections.Should().Be(100);
        kestrelOptions.Limits.MaxRequestBodySize.Should().Be(500000);
        kestrelOptions.Limits.KeepAliveTimeout.Should().Be(TimeSpan.FromSeconds(30));
    }

    [TestMethod]
    public async Task ApplyListeners_WithSingleListener_ConfiguresKestrelCorrectly()
    {
        var port = GetRandomUnusedPort();
        _builder.Listen($"http://localhost:{port}");

        var webAppBuilder = WebApplication.CreateBuilder(new WebApplicationOptions());

        webAppBuilder.WebHost.UseKestrel(_builder.ApplyListeners);

        var app = webAppBuilder.Build();
        app.MapGet("/", () => "Test Response");

        await app.StartAsync();

        var client = new HttpClient();
        var response = await client.GetAsync($"http://localhost:{port}/");
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync();

        content.Should().Be("Test Response");

        await app.StopAsync();
    }

    [TestMethod]
    public async Task ApplyListeners_WithMultipleListeners_ConfiguresKestrelWithMultipleEndpoints()
    {
        int portForAllInterfaces = GetRandomUnusedPort();
        int portForLocalhost = GetRandomUnusedPort();

        _builder.Listen($"http://0.0.0.0:{portForAllInterfaces}");
        _builder.Listen($"http://127.0.0.1:{portForLocalhost}");

        var webAppBuilder = WebApplication.CreateBuilder(new WebApplicationOptions());

        webAppBuilder.WebHost.UseKestrel(_builder.ApplyListeners);

        var app = webAppBuilder.Build();
        app.MapGet("/", () => "Test Response");

        await app.StartAsync();

        var client = new HttpClient();

        var responseFromAllInterfaces = await client.GetAsync($"http://localhost:{portForAllInterfaces}/");
        responseFromAllInterfaces.EnsureSuccessStatusCode();
        var content1 = await responseFromAllInterfaces.Content.ReadAsStringAsync();
        content1.Should().Be("Test Response");

        var responseFromLocalhost = await client.GetAsync($"http://127.0.0.1:{portForLocalhost}/");
        responseFromLocalhost.EnsureSuccessStatusCode();
        var content2 = await responseFromLocalhost.Content.ReadAsStringAsync();
        content2.Should().Be("Test Response");

        await app.StopAsync();
    }

    private static int GetRandomUnusedPort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

