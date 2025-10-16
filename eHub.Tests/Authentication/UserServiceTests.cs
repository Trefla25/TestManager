using eHub.Authentication.Service;
using eHub.Config;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace eHub.Tests.Authentication;

[TestClass]
public class UserServiceTests
{
    [TestMethod]
    public async Task GetAll_WithBasicConfig_ReturnsDummyUsers()
    {
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers =
                [
                    new DummyUser { Username = "Alice", Password = "password123" },
                    new DummyUser { Username = "Bob", Password = "qwerty" }
                ]
            }
        };

        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var users = await userService.GetAll();

        users.Should().HaveCount(2);
        users.Should().BeEquivalentTo(config.Basic.DummyUsers);
    }

    [TestMethod]
    public async Task GetAll_WhenBasicConfigIsNull_ReturnsEmpty()
    {
        var config = new AuthenticationConfig { Basic = null };
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var users = await userService.GetAll();

        users.Should().BeEmpty();
    }

    [TestMethod]
    public async Task GetAll_WhenDummyUsersEmpty_ReturnsEmpty()
    {
        var config = new AuthenticationConfig
        {
            Basic = new() { DummyUsers = [] }
        };

        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var users = await userService.GetAll();

        users.Should().BeEmpty();
    }

    [TestMethod]
    public async Task Authenticate_WithValidCredentials_ReturnsUser()
    {
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers =
                [
                    new DummyUser { Username = "Alice", Password = "password123" },
                    new DummyUser { Username = "Bob", Password = "qwerty" }
                ]
            }
        };

        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var user = await userService.Authenticate("Alice", "password123");

        user.Should().NotBeNull();
        user.Username.Should().Be("Alice");
        user.Password.Should().Be("password123");
    }

    [TestMethod]
    public async Task Authenticate_WhenDisabled_ReturnsSameUser()
    {
        var config = new AuthenticationConfig
        {
            Enabled = false,
            Basic = new() { DummyUsers = [] }
        };

        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var user = await userService.Authenticate("Alice", "password123");

        user.Should().NotBeNull(); ;
        user.Username.Should().Be("Alice");
        user.Password.Should().Be("password123");
    }

    [TestMethod]
    public async Task Authenticate_WithInvalidUsername_ReturnsNull()
    {
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = [new DummyUser { Username = "Alice", Password = "password123" }]
            }
        };

        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var user = await userService.Authenticate("alice", "password123");

        user.Should().BeNull();
    }

    [TestMethod]
    public async Task Authenticate_WithInvalidPassword_ReturnsNull()
    {
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = [new DummyUser { Username = "Alice", Password = "password123" }]
            }
        };

        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var user = await userService.Authenticate("Alice", "PASSWORD123");

        user.Should().BeNull();
    }

    [TestMethod]
    public async Task Authenticate_WhenDummyUsersEmpty_ReturnsNull()
    {
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig { DummyUsers = [] }
        };

        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var user = await userService.Authenticate("Alice", "password123");

        user.Should().BeNull();
    }


    [TestMethod]
    public async Task Authenticate_WhenBasicConfigIsNull_ReturnsNull()
    {
        var config = new AuthenticationConfig { Basic = null };
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        var user = await userService.Authenticate("Alice", "password123");

        user.Should().BeNull();
    }
}
