using eHub.Authentication.Service;
using eHub.Config;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace eHub.Tests.Authentication;

public class UserServiceTests
{
    [Fact]
    public async Task GetAll_WithBasicConfig_ReturnsDummyUsers()
    {
        // Arrange
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
        
        // Act
        var users = await userService.GetAll();
        
        // Assert
        var userArray = users.ToArray();
        userArray.Should().HaveCount(2);
        userArray.Should().BeEquivalentTo(config.Basic.DummyUsers);
    }

    [Fact]
    public async Task GetAll_WhenBasicConfigIsNull_ReturnsEmpty()
    {
        // Arrange
        var config = new AuthenticationConfig { Basic = null };
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);
        var userService = new UserService(options);

        // Act
        var users = await userService.GetAll();
        
        // Assert
        users.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAll_WhenDummyUsersEmpty_ReturnsEmpty()
    {
        // Arrange
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig { DummyUsers = [] }
        };
        
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);

        // Act
        var users = await userService.GetAll();
        
        // Assert
        users.Should().BeEmpty();
    }

    [Fact]
    public async Task Authenticate_WithValidCredentials_ReturnsUser()
    {
        // Arrange
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

        // Act
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);
        var user = await userService.Authenticate("Alice", "password123");
        // Assert
        user.Should().NotBeNull();
        user.Username.Should().Be("Alice");
        user.Password.Should().Be("password123");
    }

    [Fact]
    public async Task Authenticate_WhenDisabled_ReturnsSameUser()
    {
        // Arrange
        var config = new AuthenticationConfig
        {
            Enabled = false,
            Basic = new() { DummyUsers = [] }
        };

        // Act
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);
        var user = await userService.Authenticate("Alice", "password123");
        // Assert
        user.Should().NotBeNull();
        user.Username.Should().Be("Alice");
        user.Password.Should().Be("password123");
    }

    [Fact]
    public async Task Authenticate_WithInvalidUsername_ReturnsNull()
    {
        // Arrange
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = [new DummyUser { Username = "Alice", Password = "password123" }]
            }
        };

        // Act
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);
        var user = await userService.Authenticate("alice", "password123");
        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_WithInvalidPassword_ReturnsNull()
    {
        // Arrange
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig
            {
                DummyUsers = [new DummyUser { Username = "Alice", Password = "password123" }]
            }
        };

        // Act
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);
        var user = await userService.Authenticate("Alice", "PASSWORD123");
        // Assert
        user.Should().BeNull();
    }

    [Fact]
    public async Task Authenticate_WhenDummyUsersEmpty_ReturnsNull()
    {
        // Arrange
        var config = new AuthenticationConfig
        {
            Basic = new BasicAuthenticationConfig { DummyUsers = [] }
        };

        // Act
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        var userService = new UserService(options);
        var user = await userService.Authenticate("Alice", "password123");
        // Assert
        user.Should().BeNull();
    }


    [Fact]
    public async Task Authenticate_WhenBasicConfigIsNull_ReturnsNull()
    {
        // Arrange
        var config = new AuthenticationConfig { Basic = null };
        var options = Substitute.For<IOptionsSnapshot<AuthenticationConfig>>();
        options.Value.Returns(config);

        // Act
        var userService = new UserService(options);
        var user = await userService.Authenticate("Alice", "password123");
        // Assert
        user.Should().BeNull();
    }
}
