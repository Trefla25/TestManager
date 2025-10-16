using System.Xml.Linq;
using eHub.UI.Util;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace eHub.UI.Tests.Utils;

[TestClass]
public class XmlUtilTests
{
    [TestMethod]
    public void TryParse_WithValidXml_ReturnsTrue()
    {
        var xml = """
        <root>
            <user id='1'>
                <name>John Doe</name>
                <email>john@example.com</email>
            </user>
        </root>
        """;

        var result = XmlUtil.TryParse(xml, out var doc);

        result.Should().BeTrue();
        doc.Should().NotBeNull();
        doc.Root!.Name.LocalName.Should().Be("root");
        doc.Root.Element("user")?.Element("name")?.Value.Should().Be("John Doe");
    }

    [TestMethod]
    public void TryParse_WithInvalidXml_ReturnsFalse()
    {
        var xml = "<root><invalid></root>";

        var result = XmlUtil.TryParse(xml, out var doc);

        result.Should().BeFalse();
        doc.Should().BeNull();
    }

    [TestMethod]
    public void TryParse_WithNullOrEmpty_ReturnsFalse()
    {
        var result = XmlUtil.TryParse(string.Empty, out var doc);

        result.Should().BeFalse();
        doc.Should().NotBeNull();
        doc.Root.Should().BeNull();
    }

    [TestMethod]
    public void TryGetElementFromPath_WithValidXPath_ReturnsTrue()
    {
        var xml = """
        <root>
            <settings>
                <option name='mode'>advanced</option>
            </settings>
        </root>
        """;

        var doc = XDocument.Parse(xml);

        var result = doc.TryGetElementFromPath("/root/settings/option", out var element);

        result.Should().BeTrue();
        element.Should().NotBeNull();
        element.Value.Should().Be("advanced");
        element.Attribute("name")?.Value.Should().Be("mode");
    }

    [TestMethod]
    public void TryGetElementFromPath_WithInvalidXPath_ReturnsFalse()
    {
        var xml = "<root><item>value</item></root>";
        var doc = XDocument.Parse(xml);

        var result = doc.TryGetElementFromPath("/root/unknown", out var element);

        result.Should().BeFalse();
        element.Should().BeNull();
    }

    [TestMethod]
    public void TryGetElementFromPath_WithNullPath_ShouldReturnFalse()
    {
        var xml = "<root><item>value</item></root>";
        var doc = XDocument.Parse(xml);

        var result = doc.TryGetElementFromPath(null, out var element);

        result.Should().BeFalse();
        element.Should().BeNull();
    }
}
