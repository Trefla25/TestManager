using eHub.Config;
using eHub.PlugIn;
using eHub.Scripting.Connectors.Features;
using eHub.Scripting.Connectors;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Microsoft.Extensions.Configuration;
using eMessenger;
using System.Net;
using eHub.PlugIn.Communication;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using System.Net.Mime;
using System.Text;
using eMessenger.Tests;
using System.Text.RegularExpressions;
using eHub.Tests.Helper;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.AspNetCore.TestHost;

namespace eHub.Tests.Connectors.Http;

public class IncomingTests : IAsyncLifetime
{
    private readonly CancellationTokenSource _cancellationTokenSource = new();

    private WebApplication? _server;
    private HttpConnectorFeature _httpConnectorFeature = null!;
    private ConnectorMetadata _metadata = null!;
    private ConnectorTemplate _template = null!;
    private HttpIncoming _httpIncomingConfig = null!;
    private Uri _connectorUrl = null!;

    private IHttpConnector _httpConnector = null!;
    private IServiceProvider _serviceProvider = null!;
    private ILoggerFactory _loggerFactory = null!;
    private ILogger<HttpConnectorFeature> _logger = null!;
    private IConfiguration _configuration = null!;
    private IScopedMessenger _messenger = null!;
    private IHttpClientFactory _httpClientFactory = null!;

    public ValueTask InitializeAsync()
    {
        _httpConnector = Substitute.For<IHttpConnector>();
        _serviceProvider = Substitute.For<IServiceProvider>();
        _configuration = Substitute.For<IConfiguration>();
        _httpClientFactory = Substitute.For<IHttpClientFactory>();

        _loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = _loggerFactory.CreateLogger<HttpConnectorFeature>();
        _messenger = TestingMessenger.CreateScoped();

        _connectorUrl = new Uri($"http://localhost:{NetworkHelper.GetRandomUnusedPort()}");
        _metadata = new ConnectorMetadata(new MessagingContext(), "TestHttpConnector", "TestHttpConnectorType");

        _httpIncomingConfig = new HttpIncoming
        {
            Endpoints = [],
            Auth = new() { Enabled = false },
            Kestrel = new ConfigurationBuilder().AddInMemoryCollection([
                KeyValuePair.Create<string, string?>("Kestrel:Endpoints:Http:Url", _connectorUrl.ToString())
            ]).Build().GetSection("Kestrel")
        };

        _template = new ConnectorTemplate()
        {
            Type = "TestHttpConnectorType",
            Enabled = true,
            HttpIncoming = _httpIncomingConfig
        };
        
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Ensures that when a configured endpoint and a programmed endpoint have the same path and method, only the programmed endpoint is registered.
    /// </summary>
    [Fact]
    public async Task RegisterEndpoints_SameConfigAndSetup_UsesOnlySetup()
    {
        // Arrange
        _httpIncomingConfig.Endpoints.Add("Test", new() { Path = "/test", HttpMethod = "GET" });

        var httpSetupHandlerWasInvoked = false;
        var httpIncomingHandlerWasInvoked = false;

        // Mock HttpSetup to map the /test route
        _httpConnector.When(x => x.HttpSetup(Arg.Any<IEndpointRouteBuilder>())).Do(callInfo =>
        {
            var routeBuilder = callInfo.Arg<IEndpointRouteBuilder>();
            routeBuilder.MapGet("/test", () =>
            {
                httpSetupHandlerWasInvoked = true;
                return Results.Ok();
            });
        });

        // Mock IncomingHttpRequestHandler to simulate configured handling
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions((_, _, _) =>
        {
            httpIncomingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        }));

        // Start the feature and send an HTTP request
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Act
        var httpClient = new HttpClient() { BaseAddress = _connectorUrl };
        var response = await httpClient.GetAsync("test", _cancellationTokenSource.Token);
        
        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "Expected HTTP 200 OK from the /test endpoint.");
        httpSetupHandlerWasInvoked.Should().BeTrue("Expected the programmed (HttpSetup) endpoint to handle the request.");
        httpIncomingHandlerWasInvoked.Should().BeFalse("Expected the configured (HttpIncoming) endpoint to be ignored.");
    }

    /// <summary>
    /// Ensures that when a configured endpoint and a programmed endpoint have the same path but different methods, both the programmed and configured endpoint are registered.
    /// </summary>
    [Fact]
    public async Task RegisterEndpoints_SameConfigAndSetupPathDifferentMethods_UsesBoth()
    {
        // Arrange
        _httpIncomingConfig.Endpoints.Add("Test", new() { Path = "/test", HttpMethod = "POST" });

        var httpSetupHandlerWasInvoked = false;
        var httpIncomingHandlerWasInvoked = false;

        // Mock HttpSetup to map the /test route
        _httpConnector.When(x => x.HttpSetup(Arg.Any<IEndpointRouteBuilder>())).Do(callInfo =>
        {
            var routeBuilder = callInfo.Arg<IEndpointRouteBuilder>();
            routeBuilder.MapGet("/test", () =>
            {
                httpSetupHandlerWasInvoked = true;
                return Results.Ok();
            });
        });

        // Mock IncomingHttpRequestHandler to simulate configured handling
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions((_, _, _) =>
        {
            httpIncomingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        }));

        // Start the feature and send two HTTP requests
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Act
        var httpClient = new HttpClient { BaseAddress = _connectorUrl };
        var responseGet = await httpClient.GetAsync("test", _cancellationTokenSource.Token);
        var responsePost = await httpClient.PostAsync("test", null, _cancellationTokenSource.Token);
        
        // Assert
        responseGet.StatusCode.Should().Be(HttpStatusCode.OK, "Expected HTTP 200 OK from the GET /test endpoint.");
        responsePost.StatusCode.Should().Be(HttpStatusCode.OK, "Expected HTTP 200 OK from the POST /test endpoint.");
        httpSetupHandlerWasInvoked.Should().BeTrue("Expected the programmed (HttpSetup) endpoint to handle the request.");
        httpIncomingHandlerWasInvoked.Should().BeTrue("Expected the configured (HttpIncoming) endpoint to be ignored.");
    }

    /// <summary>
    /// Ensures that when a configured endpoint and a programmed endpoint have the same path and the programmed one is registered for all methods, the configured endpoint is not registered for any methods.
    /// </summary>
    [Fact]
    public async Task RegisterEndpoints_SameConfigAndSetupPathAllSetupMethods_UsesOnlySetup()
    {
        // Arrange
        _httpIncomingConfig.Endpoints.Add("Test", new() { Path = "/test", HttpMethod = null });

        var httpSetupHandlerWasInvoked = false;
        var httpIncomingHandlerWasInvoked = false;

        // Mock HttpSetup to map the /test route
        _httpConnector.When(x => x.HttpSetup(Arg.Any<IEndpointRouteBuilder>())).Do(callInfo =>
        {
            var routeBuilder = callInfo.Arg<IEndpointRouteBuilder>();
            routeBuilder.Map("/test", () =>
            {
                httpSetupHandlerWasInvoked = true;
                return Results.Ok();
            });
        });

        // Mock IncomingHttpRequestHandler to simulate configured handling
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions((_, _, _) =>
        {
            httpIncomingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        }));

        // Start the feature
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Act
        var httpClient = new HttpClient { BaseAddress = _connectorUrl };
        var methods = new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete, HttpMethod.Head, HttpMethod.Options, HttpMethod.Trace, HttpMethod.Patch };
        
        // Assert
        foreach (var method in methods)
        {
            var httpRequestMessage = new HttpRequestMessage(method, "test");
            var response = await httpClient.SendAsync(httpRequestMessage, _cancellationTokenSource.Token);

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"Expected HTTP 200 OK from the {method} /test endpoint.");
            httpSetupHandlerWasInvoked.Should().BeTrue("Expected the programmed (HttpSetup) endpoint to handle the request.");
            httpIncomingHandlerWasInvoked.Should().BeFalse("Expected the configured (HttpIncoming) endpoint to be ignored.");

            httpSetupHandlerWasInvoked = false;
            httpIncomingHandlerWasInvoked = false;
        }
    }

    /// <summary>
    /// Ensures that when a configured endpoint and a programmed endpoint have the same path and the configured one is registered for all methods while the programmed only for a few, the configured endpoint is registered only for the methods not used by the programmed ones.
    /// </summary>
    [Fact]
    public async Task RegisterEndpoints_SameConfigAndSetupPathAllConfigMethods_UsesSomeConfig()
    {
        // Arrange
        _httpIncomingConfig.Endpoints.Add("Test", new() { Path = "/test", HttpMethod = null });

        var httpSetupHandlerWasInvoked = false;
        var httpIncomingHandlerWasInvoked = false;

        // Mock HttpSetup to map the /test route
        _httpConnector.When(x => x.HttpSetup(Arg.Any<IEndpointRouteBuilder>())).Do(callInfo =>
        {
            var routeBuilder = callInfo.Arg<IEndpointRouteBuilder>();
            routeBuilder.MapGet("/test", () =>
            {
                httpSetupHandlerWasInvoked = true;
                return Results.Ok();
            });
        });

        // Mock IncomingHttpRequestHandler to simulate configured handling
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions((_, _, _) =>
        {
            httpIncomingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        }));

        // Start the feature
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Act
        var httpClient = new HttpClient { BaseAddress = _connectorUrl };
        var methods = new[] { HttpMethod.Get, HttpMethod.Put, HttpMethod.Post, HttpMethod.Delete, HttpMethod.Head, HttpMethod.Options, HttpMethod.Trace, HttpMethod.Patch };
        
        // Assert
        foreach (var method in methods)
        {
            var httpRequestMessage = new HttpRequestMessage(method, "test");
            var response = await httpClient.SendAsync(httpRequestMessage, _cancellationTokenSource.Token);

            var shouldInvokeSetup = method == HttpMethod.Get;
            var shouldInvokeIncoming = !shouldInvokeSetup;

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"Expected HTTP 200 OK from the {method} /test endpoint.");
            httpSetupHandlerWasInvoked.Should().Be(shouldInvokeSetup, "Expected the programmed (HttpSetup) endpoint to handle the request.");
            httpIncomingHandlerWasInvoked.Should().Be(shouldInvokeIncoming, "Expected the configured (HttpIncoming) endpoint to be ignored.");

            httpSetupHandlerWasInvoked = false;
            httpIncomingHandlerWasInvoked = false;
        }
    }

    [Fact]
    public async Task ExecuteRequest_OnPost_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test")
        {
            Content = new StringContent("{\"test\":\"post-data\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        };
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request);
    }

    [Fact]
    public async Task ExecuteRequest_OnGet_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test");
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request);
    }

    [Fact]
    public async Task ExecuteRequest_OnPut_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/test")
        {
            Content = new StringContent("{\"test\":\"put-data\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        };
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request);
    }

    [Fact]
    public async Task ExecuteRequest_OnDelete_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Delete, "/api/test");
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request);
    }

    [Fact]
    public async Task ExecuteRequest_OnPatch_ReturnsExpectedResponse()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Patch, "/api/test")
        {
            Content = new StringContent("{\"test\":\"patch-data\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        };
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request);
    }

    [Fact]
    public async Task ExecuteRequest_WhenOverridden_UseCustomHandler()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test")
        {
            Content = new StringContent("{\"test\":\"post-data\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        };

        var endpoint = new HttpConnectorEndpointConfig { Path = request.RequestUri?.OriginalString, HttpMethod = request.Method.Method, Topic = $"TestTopic" };

        // Initialize HttpConnectorFeature
        _httpIncomingConfig.Endpoints.Add($"TestEndpoint", endpoint);

        var httpIncomingHandlerWasInvoked = false;
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions(incomingHttpRequestHandler: (_, _, _) =>
        {
            httpIncomingHandlerWasInvoked = true;
            return ValueTask.FromResult(ConnectorResponses.HttpOk("TestHttpConnector"));
        }));

        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Start the feature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Send HTTP request
        var httpClient = new HttpClient() { BaseAddress = _connectorUrl };
        
        // Act
        using var response = await httpClient.SendAsync(request, _cancellationTokenSource.Token);
        
        // Assert
        // Verify the response
        httpIncomingHandlerWasInvoked.Should().BeTrue();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ExecuteRequest_WithHeaders_AllAreForwarded()
    {
        // Arrange
        var headers = new Dictionary<string, StringValues>
        {
            { "Token", Guid.NewGuid().ToString() },
            { "X-Test-Header", "test-value" },
            { "X-Multiple-Header", new StringValues(["value1", "value2", "value3"]) },
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test")
        {
            Content = new StringContent("{\"test\":\"post-data\"}", Encoding.UTF8, MediaTypeNames.Application.Json),
        };
        foreach (var header in headers)
        {
            foreach (var item in header.Value)
            {
                request.Headers.Add(header.Key, item);
            }
        }
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request);
    }

    [Fact]
    public async Task ExecuteRequest_WithQuery_AllAreForwarded()
    {
        // Arrange
        var query = new Dictionary<string, StringValues>
        {
            { "search", "example" },
            { "sort", "packets" },
            { "multi", new StringValues(["one", "two", "three"]) }
        };

        var uriWithQuery = QueryHelpers.AddQueryString("/api/test", query);
        var request = new HttpRequestMessage(HttpMethod.Post, uriWithQuery)
        {
            Content = new StringContent("{\"test\":\"post-data\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        };
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request);
    }

    [Fact]
    public async Task ExecuteRequest_WithRouteParameters_AllAreForwarded()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/test/5/test")
        {
            Content = new StringContent("{\"test\":\"post-data\"}", Encoding.UTF8, MediaTypeNames.Application.Json)
        };
        
        var endpoint = new HttpConnectorEndpointConfig { Path = "/api/test/{param1}/{param2}", HttpMethod = request.Method.Method, Topic = $"TestTopic" };
        
        // Act & Assert
        await TestIncomingHttpRequestAsync(request, endpoint);
    }

    [Fact]
    public async Task ExecuteRequest_WithOutgoingApi_RedirectsHttpRequest()
    {
        // Arrange
        const string requestContent = "{\"test\":\"post-data\"}";
        const string requestContentType = MediaTypeNames.Application.Json;
        const string expectedResponseContent = "{\"test\":\"response-data\"}";
        const string expectedResponseContentType = MediaTypeNames.Application.Json;
        const string templatePath = "/api/test/{id}";
        const string requestPath = "/api/test/5";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestPath)
        {
            Content = new StringContent(requestContent, Encoding.UTF8, requestContentType),
        };

        httpRequest.Headers.Add("X-Test-Header", "test-value");

        // Initialize test server
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions());
        builder.WebHost.UseTestServer();
        
        var server = builder.Build();
        _server = server;
        
        server.MapMethods(templatePath, [HttpMethods.Post], async context =>
        {
            using var memoryStream = new MemoryStream();
            await context.Request.Body.CopyToAsync(memoryStream);
            var contentBytes = memoryStream.ToArray();

            context.Request.ContentType.Should().StartWith(requestContentType);
            contentBytes.Should().BeEquivalentTo(Encoding.UTF8.GetBytes(requestContent));
            foreach (var header in httpRequest.Headers)
            {
                context.Request.Headers[header.Key].Should().BeEquivalentTo(header.Value);
            }

            var routeParameters = ExtractRouteValues(templatePath, context.Request.Path.ToString());
            foreach (var routeParameter in routeParameters)
            {
                context.Request.RouteValues[routeParameter.Key].Should().Be(routeParameter.Value);
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
            context.Response.ContentType = expectedResponseContentType;
            await context.Response.WriteAsync(expectedResponseContent);
        });

        await server.StartAsync(_cancellationTokenSource.Token);

        // Initialize the feature
        var endpoint = new HttpConnectorEndpointConfig { Api = "TestApi", Path = templatePath, HttpMethod = httpRequest.Method.Method, Topic = $"TestTopic" };
        _httpIncomingConfig.Endpoints.Add($"TestEndpoint", endpoint);

        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());
        _httpClientFactory.CreateClient(Arg.Is("TestApi")).Returns(server.GetTestClient());

        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Start the feature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Act
        // Send HTTP request
        var httpClient = new HttpClient() { BaseAddress = _connectorUrl };
        using var response = await httpClient.SendAsync(httpRequest, _cancellationTokenSource.Token);
        var responseContent = await response.Content.ReadAsStringAsync(_cancellationTokenSource.Token);
        var responseContentType = response.Content.Headers.ContentType?.ToString() ?? string.Empty;

        // Assert
        // Verify the response
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        responseContentType.Should().Be(expectedResponseContentType);
        responseContent.Should().Be(expectedResponseContent);
    }

    [Fact]
    public async Task StartAsync_WithInterpolatedEndpoints_FillsPlaceholders()
    {
        // Arrange
        var endpoint = new HttpConnectorEndpointConfig
        {
            Path = "/api/{endpointKey}/{connectorName}",
            HttpMethod = HttpMethods.Post,
            Topic = "topic/{endpointKey}/{connectorName}"
        };

        _httpIncomingConfig.Endpoints.Add("TestEndpoint", endpoint);
        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());

        // Initialize and start HttpConnectorFeature
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        
        // Act
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        _httpIncomingConfig.Endpoints["TestEndpoint"].Topic.Should().Be("topic/TestEndpoint/TestHttpConnector");
        _httpIncomingConfig.Endpoints["TestEndpoint"].Path.Should().Be("/api/TestEndpoint/TestHttpConnector");
    }

    [Fact]
    public async Task StartAsync_WithAllPlaceholders_FillsPlaceholders()
    {
        // Arrange
        var endpoint = new HttpConnectorEndpointConfig
        {
            Path = "/api/{endpointKey}/{topic}/{api}/{httpMethod}/{connectorName}/{connectorType}",
            HttpMethod = HttpMethods.Post,
            Topic = "topic/{endpointKey}/{api}/{httpMethod}/{connectorName}/{connectorType}",
            Api = "TestApi"
        };

        // Add endpoint configuration
        _httpIncomingConfig.Endpoints.Add("TestEndpoint", endpoint);

        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());

        // Initialize and start HttpConnectorFeature
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);
        
        // Act
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);
        
        // Assert
        _httpIncomingConfig.Endpoints["TestEndpoint"].Topic.Should().Be("topic/TestEndpoint/TestApi/POST/TestHttpConnector/TestHttpConnectorType");
        _httpIncomingConfig.Endpoints["TestEndpoint"].Path.Should().Be("/api/TestEndpoint/topic/TestEndpoint/TestApi/POST/TestHttpConnector/TestHttpConnectorType/TestApi/POST/TestHttpConnector/TestHttpConnectorType");
    }

    private async Task TestIncomingHttpRequestAsync(HttpRequestMessage request, HttpConnectorEndpointConfig? endpoint = null)
    {
        endpoint ??= new HttpConnectorEndpointConfig
        {
            Path = request.RequestUri?.ToString().Split('?')[0],
            HttpMethod = request.Method.Method,
            Topic = $"TestTopic"
        };

        // Start the test listener
        const int responseStatus = StatusCodes.Status201Created;
        const string responseContentType = MediaTypeNames.Application.Json;
        const string responseContent = "{\"test\":\"response-data\"}";
        await _messenger.AnswerAsync<ConnectorRequest, ConnectorResponse>(endpoint.Topic!, async (r) =>
        {
            r.Http.Should().NotBeNull();
            r.ContentType.Should().Be(request.Content?.Headers.ContentType?.ToString() ?? MediaTypeNames.Application.Octet);

            var requestContent = request.Content is not null ? await request.Content!.ReadAsByteArrayAsync() : [];
            r.Content.ToArray().Should().BeEquivalentTo(requestContent);

            foreach (var header in request.Headers)
            {
                r.Http.Headers.Should().ContainKey(header.Key);
                r.Http.Headers[header.Key].Should().BeEquivalentTo(header.Value.Join(", "));
            }

            var routeParameters = ExtractRouteValues(endpoint.Path, request.RequestUri?.AbsoluteUri);

            foreach (var routeParameter in routeParameters)
            {
                r.Http.RouteParameters.Should().ContainKey(routeParameter.Key);
                r.Http.RouteParameters[routeParameter.Key].Should().BeEquivalentTo(routeParameter.Value);
            }

            var query = QueryHelpers.ParseQuery(request.RequestUri?.Query ?? "");
            foreach (var queryParam in query)
            {
                r.Http.Query.Should().ContainKey(queryParam.Key);
                r.Http.Query[queryParam.Key].Should().BeEquivalentTo(queryParam.Value);
            }

            return ConnectorResponses.HttpContent(responseContent, responseContentType, "MyConnector", responseStatus);
        });

        // Initialize HttpConnectorFeature
        _httpIncomingConfig.Endpoints.Add($"TestEndpoint", endpoint);

        _httpConnector.HttpConnectorOptions.Returns(new HttpConnectorOptions());
        _httpConnectorFeature = new HttpConnectorFeature(_serviceProvider, _logger, _loggerFactory, _configuration, _messenger, _httpClientFactory, _httpConnector, _metadata, _template);

        // Start HttpConnectorFeature
        await _httpConnectorFeature.StartAsync(_cancellationTokenSource.Token);

        // Send HTTP request
        var httpClient = new HttpClient() { BaseAddress = _connectorUrl };
        using var response = await httpClient.SendAsync(request, _cancellationTokenSource.Token);

        // Verify the response
        response.StatusCode.Should().Be((HttpStatusCode)responseStatus);
        response.Content.Headers.ContentType!.ToString().Should().Be(responseContentType);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().BeEquivalentTo(responseContent);
    }

    private static Dictionary<string, string> ExtractRouteValues(string? template, string? actualPath)
    {
        if(template is null || actualPath is null)
        {
            return [];
        }

        var pattern = Regex.Replace(template, @"\{(\w+)\}", @"(?<$1>[^/]+)");
        pattern = $"{pattern}$";

        var match = Regex.Match(actualPath, pattern);
        if (!match.Success)
        {
            return [];
        }

        var routeValues = new Dictionary<string, string>();
        foreach (var groupName in match.Groups.Keys.Where(g => g != "0"))
        {
            routeValues[groupName] = match.Groups[groupName].Value;
        }

        return routeValues;
    }
    
    public async ValueTask DisposeAsync()
    {
        if (_server is not null)
        {
            await _server.StopAsync();
        }

        await _httpConnectorFeature.DisposeAsync();

        await _cancellationTokenSource.CancelAsync();
        _cancellationTokenSource.Dispose();
    }
}
