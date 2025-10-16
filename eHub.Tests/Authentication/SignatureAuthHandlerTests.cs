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

[TestClass]
public class SignatureAuthHandlerTests
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

    private IOptionsMonitor<AuthenticationSchemeOptions> _options = default!;
    private ILoggerFactory _loggerFactory = default!;
    private UrlEncoder _encoder = default!;
    private SignatureAuthenticationHandlerSpy _handlerSpy = default!;

    [TestInitialize]
    public async Task TestInitialize()
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

    [TestMethod]
    public async Task HandleAuthenticateAsync_NoHeaders_ReturnsNoResult()
    {
        _handlerSpy.HttpContext.Request.Headers.Clear();

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Should().Be(AuthenticateResult.NoResult());
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
        result.Principal.Should().BeNull();
        result.Ticket.Should().BeNull();
        result.Failure.Should().BeNull();
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_OnlyPacketIdHeader_ReturnsNoResult()
    {
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.PacketIdHeader, "123");

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Should().Be(AuthenticateResult.NoResult());
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
        result.Principal.Should().BeNull();
        result.Ticket.Should().BeNull();
        result.Failure.Should().BeNull();
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_OnlySignatureHeader_ReturnsNoResult()
    {
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.SignatureHeader, "signature");

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Should().Be(AuthenticateResult.NoResult());
        result.Succeeded.Should().BeFalse();
        result.None.Should().BeTrue();
        result.Principal.Should().BeNull();
        result.Ticket.Should().BeNull();
        result.Failure.Should().BeNull();
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_WithBothHeaders_ReturnsSuccess()
    {
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.PacketIdHeader, "123");
        _handlerSpy.HttpContext.Request.Headers.TryAdd(ConnectorHeaders.SignatureHeader, "signature");

        var result = await _handlerSpy.InvokeAuthenticateAsync();

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
}
