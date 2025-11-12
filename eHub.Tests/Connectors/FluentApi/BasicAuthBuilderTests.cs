using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

public class BasicAuthBuilderTests
{
    private readonly BasicAuthBuilder _builder = new();

    [Fact]
    public void AddDummyUser_WithValidUser_AddsDummyUser()
    {
        // Arrange
        _builder.AddDummyUser("dummyUser", "dummyPass");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.DummyUsers.Should().ContainSingle(user => user.Username == "dummyUser" && user.Password == "dummyPass");
    }

    [Fact]
    public void Build_WithMultipleDummyUsers_AddsAllDummyUsers()
    {
        // Arrange
        _builder
            .AddDummyUser("user1", "pass1")
            .AddDummyUser("user2", "pass2");
        
        // Act
        var config = _builder.Build();
        
        // Assert
        config.DummyUsers.Should().HaveCount(2);
        config.DummyUsers.Should().Contain(user => user.Username == "user1" && user.Password == "pass1");
        config.DummyUsers.Should().Contain(user => user.Username == "user2" && user.Password == "pass2");
    }
}
