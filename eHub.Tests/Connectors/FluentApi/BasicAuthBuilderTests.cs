using eHub.Scripting.Connectors.Configuration;
using FluentAssertions;

namespace eHub.Tests.Connectors.FluentApi;

[TestClass]
public class BasicAuthBuilderTests
{
    private readonly BasicAuthBuilder _builder = new();

    [TestMethod]
    public void AddDummyUser_WithValidUser_AddsDummyUser()
    {
        _builder.AddDummyUser("dummyUser", "dummyPass");

        var config = _builder.Build();

        config.DummyUsers.Should().ContainSingle(user => user.Username == "dummyUser" && user.Password == "dummyPass");
    }

    [TestMethod]
    public void Build_WithMultipleDummyUsers_AddsAllDummyUsers()
    {
        _builder
            .AddDummyUser("user1", "pass1")
            .AddDummyUser("user2", "pass2");

        var config = _builder.Build();

        config.DummyUsers.Should().HaveCount(2);
        config.DummyUsers.Should().Contain(user => user.Username == "user1" && user.Password == "pass1");
        config.DummyUsers.Should().Contain(user => user.Username == "user2" && user.Password == "pass2");
    }
}
