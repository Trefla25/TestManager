using System.Text.Encodings.Web;
using eHub.Authentication;
using eHub.PlugIn.Communication;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace eHub.Tests.Authentication;

public class SignatureAuthHandlerTests : IAsyncLifetime
{
    private class SignatureAuthenticationHandlerSpy(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder)
        : SignatureAuthenticationHandler(options, loggerFactory, encoder)
    {
        public HttpContext HttpContext => Context;

        public Task<AuthenticateResult> InvokeAuthenticateAsync() => HandleAuthenticateAsync();
    }

    private IOptionsMonitor<AuthenticationSchemeOptions> _options = null!;
    private ILoggerFactory _loggerFactory = null!;
    private UrlEncoder _encoder = null!;
    private SignatureAuthenticationHandlerSpy _handlerSpy = null!;

    public async ValueTask InitializeAsync()
    {
        _options = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _encoder = UrlEncoder.Default;

        _options.Get(SignatureAuthenticationHandler.SchemeName).Returns(new AuthenticationSchemeOptions());

        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme(
            SignatureAuthenticationHandler.SchemeName,
            SignatureAuthenticationHandler.SchemeName,
            typeof(SignatureAuthenticationHandler));


        _handlerSpy = new SignatureAuthenticationHandlerSpy(_options, _loggerFactory, _encoder);
        await _handlerSpy.InitializeAsync(scheme, httpContext);
    }

    [Fact]
    public async Task HandleAuthenticateAsync_NoHeaders_ReturnsNoResult()
    {
        // Arrange
        _handlerSpy.HttpContext.Request.Headers.Clear();
        
        // Act
        var result = await _handlerSpy.InvokeAuthenticateAsync();
        
        // Assert
        result.Should().Be(AuthenticateResult.NoResult());
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
        result.Principal.Should().BeNull();
        result.Ticket.Should().BeNull();
        result.Failure.Should().BeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_OnlyPacketIdHeader_ReturnsNoResult()
    {
        // Arrange
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.PacketIdHeader, "123");
        
        // Act
        var result = await _handlerSpy.InvokeAuthenticateAsync();
        
        // Assert
        result.Should().Be(AuthenticateResult.NoResult());
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
        result.Principal.Should().BeNull();
        result.Ticket.Should().BeNull();
        result.Failure.Should().BeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_OnlySignatureHeader_ReturnsNoResult()
    {
        // Arrange
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.SignatureHeader, "signature");
        
        // Act
        var result = await _handlerSpy.InvokeAuthenticateAsync();
        
        // Assert
        result.Should().Be(AuthenticateResult.NoResult());
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
        result.Principal.Should().BeNull();
        result.Ticket.Should().BeNull();
        result.Failure.Should().BeNull();
    }

    [Fact]
    public async Task HandleAuthenticateAsync_WithBothHeaders_ReturnsSuccess()
    {
        // Arrange
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.PacketIdHeader, "123");
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.SignatureHeader, "signature");
        
        // Act
        var result = await _handlerSpy.InvokeAuthenticateAsync();
        
        // Assert
        result.Succeeded.Should().BeTrue();
        result.Succeeded.Should().BeTrue();
        result.None.Should().BeFalse();
        result.Failure.Should().BeNull();

        result.Principal.Should().NotBeNull();
        result.Principal.Identity.Should().NotBeNull();
        result.Principal.Identity.Name.Should().Be("eHub");

        result.Ticket.Should().NotBeNull();
        result.Ticket.AuthenticationScheme.Should().Be(SignatureAuthenticationHandler.SchemeName);
    }
    
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
