using System.Xml.Linq;
using eHub.UI.Util;
using FluentAssertions;
using Xunit;

namespace eHub.UI.Tests.Utils;

public class XmlUtilTests
{
    [Fact]
    public void TryParse_WithValidXml_ReturnsTrue()
    {
        // Arrange
        var xml = """
        <root>
            <user id='1'>
                <name>John Doe</name>
                <email>john@example.com</email>
            </user>
        </root>
        """;
        // Act
        var result = XmlUtil.TryParse(xml, out var doc);
        // Assert
        result.Should().BeTrue();
        doc.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("root");
        doc.Root.Element("user")?.Element("name")?.Value.Should().Be("John Doe");
    }

    [Fact]
    public void TryParse_WithInvalidXml_ReturnsFalse()
    {
        // Arrange
        var xml = "<root><invalid></root>";
        // Act
        var result = XmlUtil.TryParse(xml, out var doc);
        // Assert
        result.Should().BeFalse();
        doc.Should().BeNull();
    }

    [Fact]
    public void TryParse_WithNullOrEmpty_ReturnsFalse()
    {
        // Arrange
        var result = XmlUtil.TryParse(string.Empty, out var doc);
        // Assert
        result.Should().BeFalse();
        doc.Should().NotBeNull();
        doc.Root.Should().BeNull();
    }

    [Fact]
    public void TryGetElementFromPath_WithValidXPath_ReturnsTrue()
    {
        // Arrange
        var xml = """
        <root>
            <settings>
                <option name='mode'>advanced</option>
            </settings>
        </root>
        """;

        // Act
        var doc = XDocument.Parse(xml);
        var result = doc.TryGetElementFromPath("/root/settings/option", out var element);
        // Assert
        result.Should().BeTrue();
        element.Should().NotBeNull();
        element.Value.Should().Be("advanced");
        element.Attribute("name")?.Value.Should().Be("mode");
    }

    [Fact]
    public void TryGetElementFromPath_WithInvalidXPath_ReturnsFalse()
    {
        // Arrange
        var xml = "<root><item>value</item></root>";
        var doc = XDocument.Parse(xml);
        // Act
        var result = doc.TryGetElementFromPath("/root/unknown", out var element);
        // Assert
        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [Fact]
    public void TryGetElementFromPath_WithNullPath_ShouldReturnFalse()
    {
        // Arrange
        var xml = "<root><item>value</item></root>";
        var doc = XDocument.Parse(xml);
        // Act
        var result = doc.TryGetElementFromPath(null, out var element);
        // Assert
        result.Should().BeFalse();
        element.Should().BeNull();
    }
}