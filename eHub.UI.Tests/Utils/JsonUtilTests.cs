using System.Text.Json;
using eHub.UI.Util;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eHub.UI.Tests.Utils;

[TestClass]
public class JsonUtilTests
{
    [TestMethod]
    public void TryParse_WithValidJson_ReturnsTrue()
    {
        var json = """
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

        var result = JsonUtil.TryParse(json, out var document);

        result.Should().BeTrue();
        document.Should().NotBeNull();

        var root = document.RootElement;

        root.GetProperty("person").GetProperty("name").GetString().Should().Be("John");
        root.GetProperty("person").GetProperty("age").GetInt32().Should().Be(30);
        root.GetProperty("person").GetProperty("address").GetProperty("street").GetString().Should().Be("123 Main St");
        root.GetProperty("person").GetProperty("hobbies")[1].GetString().Should().Be("gaming");
    }

    [TestMethod]
    public void TryParse_WithNullJson_ReturnsFalse()
    {
        var result = JsonUtil.TryParse(null, out var document);

        result.Should().BeFalse();
        document.Should().BeNull();
    }

    [TestMethod]
    public void TryParse_WithEmptyJson_ReturnsFalse()
    {
        var result = JsonUtil.TryParse(string.Empty, out var document);

        result.Should().BeFalse();
        document.Should().BeNull();
    }

    [TestMethod]
    public void TryParse_WithInvalidJson_ReturnsFalse()
    {
        var invalidJson = "{\"key\": \"value\"";

        var result = JsonUtil.TryParse(invalidJson, out var document);

        result.Should().BeFalse();
        document.Should().BeNull();
    }

    [TestMethod]
    public void TryGetElementFromPath_WithValidPath_ReturnsFalse()
    {
        var json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        var result = document.TryGetElementFromPath("person.name", out var element);

        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [TestMethod]
    public void TryGetElementFromPath_WithNullPath_ReturnsFalse()
    {
        var json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        var result = document.TryGetElementFromPath(null, out var element);

        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [TestMethod]
    public void TryGetElementFromPath_WithInvalidPath_ReturnsFalse()
    {
        var json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        var result = document.TryGetElementFromPath("person.invalidPath", out var element);

        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [TestMethod]
    public void TryGetElementFromPath_WithEmptyPath_ReturnsFalse()
    {
        var json = "{\"person\": { \"name\": \"John\" }}";
        var document = JsonDocument.Parse(json);

        var result = document.TryGetElementFromPath(string.Empty, out var element);

        result.Should().BeFalse();
        element.Should().BeNull();
    }
}
