using eHub.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace eHub.Tests;

public class DebugRequestLogTests
{
    private readonly DefaultHttpContext _context = new();
    private readonly RequestDelegate _next = Substitute.For<RequestDelegate>();
    private readonly ILogger<DebugRequestLog> _logger = Substitute.For<ILogger<DebugRequestLog>>();
    
    [Fact]
    public async Task InvokeAsync_WhenTraceIsEnabled_LogsRequestDetails()
    {
        // Arrange
        _logger.IsEnabled(LogLevel.Trace).Returns(true);
        _context.Request.Method = "GET";
        _context.Request.Path = "/test/path";
        _context.Request.Protocol = "HTTP/1.1";
        _context.Request.Headers["X-Custom"] = "Value";

        var middleware = new DebugRequestLog(_logger, _next);

        string? capturedLog = null;

        _logger.When(x => x.Log(
            LogLevel.Trace,
            Arg.Any<EventId>(),
            Arg.Do<object>(state => capturedLog = state.ToString()),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        )).Do(_ => { });

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog.Should().Contain("GET /test/path HTTP/1.1");
        capturedLog.Should().Contain("X-Custom: Value");

        await _next.Received(1).Invoke(_context);
    }    

    [Fact]
    public async Task InvokeAsync_WhenTraceIsDisabled_SkipsLoggingButCallsNext()
    {
        // Arrange
        _logger.IsEnabled(LogLevel.Trace).Returns(false);
        _context.Request.Method = "POST";
        _context.Request.Path = "/api/data";

        var middleware = new DebugRequestLog(_logger, _next);

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        _logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>());

        await _next.Received(1).Invoke(_context);
    }   

    [Fact]
    public async Task InvokeAsync_WithNoHeaders_LogsRequestDetails()
    {
        // Arrange
        _logger.IsEnabled(LogLevel.Trace).Returns(true);
        _context.Request.Method = "PUT";
        _context.Request.Path = "/no/headers";
        _context.Request.Protocol = "HTTP/1.1";

        var middleware = new DebugRequestLog(_logger, _next);
        string? capturedLog = null;

        _logger.When(x => x.Log(
            LogLevel.Trace,
            Arg.Any<EventId>(),
            Arg.Do<object>(state => capturedLog = state.ToString()),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        )).Do(_ => { });

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog.Should().Contain("PUT /no/headers HTTP/1.1");

        await _next.Received(1).Invoke(_context);
    }  
    
    [Fact]
    public async Task InvokeAsync_WithMultipleHeaderValues_LogsRequestDetails()
    {
        // Arrange
        _logger.IsEnabled(LogLevel.Trace).Returns(true);
        _context.Request.Method = "PATCH";
        _context.Request.Path = "/multi/header";
        _context.Request.Protocol = "HTTP/1.1";
        _context.Request.Headers["X-Multi"] = new[] { "val1", "val2" };

        var middleware = new DebugRequestLog(_logger, _next);
        string? capturedLog = null;

        _logger.When(x => x.Log(
            LogLevel.Trace,
            Arg.Any<EventId>(),
            Arg.Do<object>(state => capturedLog = state.ToString()),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        )).Do(_ => { });

        // Act
        await middleware.InvokeAsync(_context);

        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog.Should().Contain("PATCH /multi/header HTTP/1.1");
        capturedLog.Should().Contain("X-Multi: val1,val2");

        await _next.Received(1).Invoke(_context);
    }

    [Theory]
    [InlineData("GET")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task InvokeAsync_WithAnyHttpMethod_LogsRequestDetails(string method)
    {
        // Arrange
        _logger.IsEnabled(LogLevel.Trace).Returns(true);
        _context.Request.Method = method;
        _context.Request.Path = "/test";
        _context.Request.Protocol = "HTTP/1.1";

        var middleware = new DebugRequestLog(_logger, _next);
        string? capturedLog = null;

        _logger.When(x => x.Log(
            LogLevel.Trace,
            Arg.Any<EventId>(),
            Arg.Do<object>(state => capturedLog = state.ToString()),
            Arg.Any<Exception>(),
            Arg.Any<Func<object, Exception?, string>>()
        )).Do(_ => { });

        // Act
        await middleware.InvokeAsync(_context);
        
        // Assert
        capturedLog.Should().NotBeNull();
        capturedLog.Should().Contain($"{method} /test HTTP/1.1");

        await _next.Received(1).Invoke(_context);
    }
}
