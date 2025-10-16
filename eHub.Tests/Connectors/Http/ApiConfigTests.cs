using eHub.Config;
using FluentAssertions;

namespace eHub.Tests.Connectors.Http;

[TestClass]
public class ApiConfigTests
{
    [DataTestMethod]
    [DataRow("429", 429, true)]
    [DataRow("429", 404, false)]
    [DataRow("503", 503, true)]
    [DataRow("503", 500, false)]
    [DataRow("4XX", 404, true)]
    [DataRow("4XX", 422, true)]
    [DataRow("4XX", 503, false)]
    [DataRow("5X3", 503, true)]
    [DataRow("5X3", 523, true)]
    [DataRow("5X3", 500, false)]
    [DataRow("503", 404, false)]
    [DataRow("4XX", 500, false)]
    [DataRow("5X3", 501, false)]
    [DataRow("40", 400, false)]
    [DataRow("4000", 400, false)]
    [DataRow("XXX", 123, true)]
    [DataRow("XXX", 999, true)]
    [DataRow("4xX", 429, true)]
    [DataRow("xXx", 123, true)]
    [DataRow("" , 500, false)]
    public void ShouldRetryStatus_WithStatusPattern_ReturnsExpected(string pattern, int statusCode, bool expected)
    {
        var config = new RetryPolicyConfig { ResponseStatuses = [pattern] };

        var result = config.ShouldRetryStatus(statusCode);

        result.Should().Be(expected);
    }

    [DataTestMethod]
    [DataRow(200)]
    [DataRow(500)]
    [DataRow(0)]
    public void ShouldRetryStatus_WithoutPatterns_ReturnsFalse(int statusCode)
    {
        var config = new RetryPolicyConfig();

        var result = config.ShouldRetryStatus(statusCode);

        result.Should().Be(false);
    }
}
