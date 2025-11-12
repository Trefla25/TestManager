using System.Text.Json;
using eHub.UI.Util;
using FluentAssertions;

namespace eHub.UI.Tests.Utils;

public class JsonUtilTests
{
    [Fact]
    public void TryParse_WithValidJson_ReturnsTrue()
    {
        // Arrange
        const string json = """
                            {
                                "person": {
                                    "name": "John",
                                    "age": 30,
                                    "address": {
                                        "street": "123 Main St",
                                        "city": "Anytown"
                                    },
                                    "hobbies": ["reading", "gaming"]
                                }
                            }
                            """;

        // Act
        var result = JsonUtil.TryParse(json, out var document);

        // Assert
        result.Should().BeTrue();
        document.Should().NotBeNull();

        var root = document.RootElement;

        root.GetProperty("person").GetProperty("name").GetString().Should().Be("John");
        root.GetProperty("person").GetProperty("age").GetInt32().Should().Be(30);
        root.GetProperty("person").GetProperty("address").GetProperty("street").GetString().Should().Be("123 Main St");
        root.GetProperty("person").GetProperty("hobbies")[1].GetString().Should().Be("gaming");
    }

    [Fact]
    public void TryParse_WithNullJson_ReturnsFalse()
    {
        // Act
        var result = JsonUtil.TryParse(null, out var document);

        // Assert
        result.Should().BeFalse();
        document.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithEmptyJson_ReturnsFalse()
    {
        // Act
        var result = JsonUtil.TryParse(string.Empty, out var document);

        // Assert
        result.Should().BeFalse();
        document.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithInvalidJson_ReturnsFalse()
    {
        // Arrange
        const string invalidJson = "{\"key\": \"value\"";

        // Act
        var result = JsonUtil.TryParse(invalidJson, out var document);

        // Assert
        result.Should().BeFalse();
        document.Should().BeNull();
    }

    [Fact]
    public void TryGetElementFromPath_WithValidPath_ReturnsFalse()
    {
        // Arrange
        const string json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        // Act
        var result = document.TryGetElementFromPath("person.name", out var element);

        // Assert
        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [Fact]
    public void TryGetElementFromPath_WithNullPath_ReturnsFalse()
    {
        // Arrange
        const string json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        // Act
        var result = document.TryGetElementFromPath(null, out var element);

        // Assert
        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [Fact]
    public void TryGetElementFromPath_WithInvalidPath_ReturnsFalse()
    {
        // Arrange
        const string json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        // Act
        var result = document.TryGetElementFromPath("person.invalidPath", out var element);

        // Assert
        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [Fact]
    public void TryGetElementFromPath_WithEmptyPath_ReturnsFalse()
    {
        // Arrange
        const string json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        // Act
        var result = document.TryGetElementFromPath(string.Empty, out var element);

        // Assert
        result.Should().BeFalse();
        element.Should().BeNull();
    }
}