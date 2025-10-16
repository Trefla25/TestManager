using eHub.Authentication;
using eHub.Authentication.Models;
using eHub.Authentication.Service;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;

namespace eHub.Tests.Authentication;

[TestClass]
public class BasicAuthHandlerTests
{
    private class BasicAuthenticationHandlerSpy(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory loggerFactory,
        UrlEncoder encoder,
        IUserService userService)
        : BasicAuthenticationHandler(options, loggerFactory, encoder, userService)
    {
        public HttpContext HttpContext => Context;

        public Task<AuthenticateResult> InvokeAuthenticateAsync() => HandleAuthenticateAsync();
    }

    private IOptionsMonitor<AuthenticationSchemeOptions> _options = default!;
    private ILoggerFactory _loggerFactory = default!;
    private UrlEncoder _encoder = default!;
    private IUserService _userService = default!;
    private BasicAuthenticationHandlerSpy _handlerSpy = default!;

    [TestInitialize]
    public async Task TestInitialize()
    {
        _options = Substitute.For<IOptionsMonitor<AuthenticationSchemeOptions>>();
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _userService = Substitute.For<IUserService>();
        _encoder = UrlEncoder.Default;

        _options.Get(BasicAuthenticationHandler.SchemeName).Returns(new AuthenticationSchemeOptions());

        var httpContext = new DefaultHttpContext();
        var scheme = new AuthenticationScheme(
            BasicAuthenticationHandler.SchemeName,
            BasicAuthenticationHandler.SchemeName,
            typeof(BasicAuthenticationHandler));


        _handlerSpy = new BasicAuthenticationHandlerSpy(_options, _loggerFactory, _encoder, _userService);
        await _handlerSpy.InitializeAsync(scheme, httpContext);
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_NoAuthorizationHeader_Fails()
    {
        _handlerSpy.HttpContext.Request.Headers.Clear();

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.None.Should().BeFalse();
        result.Failure.Message.Should().Be("Authorization header missing.");

        var wwwAuthenticate = _handlerSpy.HttpContext.Response.Headers.WWWAuthenticate;
        wwwAuthenticate.Should().ContainSingle();
        wwwAuthenticate.Single().Should().Be("Basic");
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_WithInvalidSchemePrefix_Fails()
    {
        _handlerSpy.HttpContext.Request.Headers.Authorization = "Bearer xyz";

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.None.Should().BeFalse();
        result.Failure.Message.Should().Be("Authorization code not formatted properly.");

        _handlerSpy.HttpContext.Response.Headers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_WithMalformedBase64_Fails()
    {
        _handlerSpy.HttpContext.Request.Headers.Authorization = "Basic $$$notbase64$$$";

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.None.Should().BeFalse();
        result.Failure.Message.Should().Be("Authorization data not formatted properly.");

        _handlerSpy.HttpContext.Response.Headers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_OnlyUsername_Fails()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("username"));
        _handlerSpy.HttpContext.Request.Headers.Authorization = $"Basic {token}";

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.None.Should().BeFalse();
        result.Failure.Message.Should().Be("Authorization data not formatted properly.");

        _handlerSpy.HttpContext.Response.Headers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_WithInvalidCredentials_Fails()
    {
        // arrange: base64 of "bob:secret"
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("username:password"));
        _handlerSpy.HttpContext.Request.Headers.Authorization = $"Basic {token}";

        _userService.Authenticate("username", "password").Returns(Task.FromResult<User?>(null));

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        await _userService.Received(1).Authenticate("username", "password");
        result.Succeeded.Should().BeFalse();
        result.Failure.Should().NotBeNull();
        result.None.Should().BeFalse();
        result.Failure.Message.Should().Be("The username or password is not correct.");

        _handlerSpy.HttpContext.Response.Headers.Should().BeEmpty();
    }

    [TestMethod]
    public async Task HandleAuthenticateAsync_WithValidCredentials_ReturnsSuccess()
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes("username:password"));
        _handlerSpy.HttpContext.Request.Headers.Authorization = $"Basic {token}";

        var fakeUser = new User
        {
            Id = 999,
            FirstName = "John",
            LastName = "Doe",
            Username = "username",
            Password = "password"
        };

        _userService.Authenticate("username", "password").Returns(Task.FromResult<User?>(fakeUser));

        var result = await _handlerSpy.InvokeAuthenticateAsync();

        result.Succeeded.Should().BeTrue();
        result.None.Should().BeFalse();
        result.Failure.Should().BeNull();
        result.Principal.Should().NotBeNull();
        result.Principal.Identity.Should().BeAssignableTo<ClaimsIdentity>();
        result.Principal.Identity.Name.Should().Be(fakeUser.Username);
        result.Ticket.Should().NotBeNull();
        result.Ticket.AuthenticationScheme.Should().Be(BasicAuthenticationHandler.SchemeName);

        var identity = (ClaimsIdentity)result.Principal.Identity;
        var identifierClaim = identity.FindFirst(ClaimTypes.NameIdentifier);

        identity.AuthenticationType.Should().Be(BasicAuthenticationHandler.SchemeName);
        identifierClaim.Should().NotBeNull();
        identifierClaim.Value.Should().Be(fakeUser.Id.ToString());
    }
}
