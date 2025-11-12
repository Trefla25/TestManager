using eHub.Config;
using FluentAssertions;

namespace eHub.Tests.Connectors.Http;

public class ApiConfigTests
{
    [Theory]
    [InlineData("429", 429, true)]
    [InlineData("429", 404, false)]
    [InlineData("503", 503, true)]
    [InlineData("503", 500, false)]
    [InlineData("4XX", 404, true)]
    [InlineData("4XX", 422, true)]
    [InlineData("4XX", 503, false)]
    [InlineData("5X3", 503, true)]
    [InlineData("5X3", 523, true)]
    [InlineData("5X3", 500, false)]
    [InlineData("503", 404, false)]
    [InlineData("4XX", 500, false)]
    [InlineData("5X3", 501, false)]
    [InlineData("40", 400, false)]
    [InlineData("4000", 400, false)]
    [InlineData("XXX", 123, true)]
    [InlineData("XXX", 999, true)]
    [InlineData("4xX", 429, true)]
    [InlineData("xXx", 123, true)]
    [InlineData("" , 500, false)]
    public void ShouldRetryStatus_WithStatusPattern_ReturnsExpected(string pattern, int statusCode, bool expected)
    {
        // Arrange
        var config = new RetryPolicyConfig { ResponseStatuses = [pattern] };
        
        // Act
        var result = config.ShouldRetryStatus(statusCode);

        // Assert
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(500)]
    [InlineData(0)]
    public void ShouldRetryStatus_WithoutPatterns_ReturnsFalse(int statusCode)
    {
        // Arrange
        var config = new RetryPolicyConfig();
        
        // Act
        var result = config.ShouldRetryStatus(statusCode);

        // Assert
        result.Should().Be(false);
    }
}
