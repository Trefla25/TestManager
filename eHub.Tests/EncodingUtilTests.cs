using eHub.PlugIn.Communication;
using FluentAssertions;
using System.Text;

namespace eHub.Tests;

[TestClass]
public class EncodingUtilTests
{
    private const string TextUtf8 = "Sömé UTF8 tëxt.";
    private const string TextAscii = "Plain ASCII text.";

    [TestMethod]
    public void TrimBom_WithUtf8Bom_RemovesPreamble()
    {
        var bom = Encoding.UTF8.GetPreamble();
        var content = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(content);

        var result = EncodingUtil.TrimBom(memory, "application/json; charset=utf-8");
        var decoded = Encoding.UTF8.GetString(result.Span);

        decoded.Should().Be(TextUtf8);
    }

    [TestMethod]
    public void TrimBom_WithoutBom_ReturnsOriginal()
    {
        var content = Encoding.UTF8.GetBytes(TextUtf8);
        var result = EncodingUtil.TrimBom(content, "application/json; charset=utf-8");

        result.ToArray().Should().BeEquivalentTo(content);
    }

    [TestMethod]
    public void TrimBom_WithNullContentType_DefaultsToUtf8()
    {
        var bom = Encoding.UTF8.GetPreamble();
        var contentBytes = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(contentBytes);

        var result = EncodingUtil.TrimBom(memory, null);
        var decoded = Encoding.UTF8.GetString(result.Span);

        decoded.Should().Be(TextUtf8);
    }

    [TestMethod]
    public void TrimBom_WithMissingCharset_DefaultsToUtf8()
    {
        var bom = Encoding.UTF8.GetPreamble();
        var contentBytes = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(contentBytes);

        var result = EncodingUtil.TrimBom(memory, "application/json");
        var decoded = Encoding.UTF8.GetString(result.Span);

        decoded.Should().Be(TextUtf8);
    }

    [TestMethod]
    public void TrimBom_WithInvalidContentType_FallbacksToUtf8()
    {
        var bom = Encoding.UTF8.GetPreamble();
        var contentBytes = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(contentBytes);

        var result = EncodingUtil.TrimBom(memory, "invalid/type; charset=invalid");
        var decoded = Encoding.UTF8.GetString(result.Span);

        decoded.Should().Be(TextUtf8);
    }

    [TestMethod]
    public void TrimBom_WithAsciiEncoding_ReturnsOriginal()
    {
        var bytes = Encoding.ASCII.GetBytes(TextAscii);
        var result = EncodingUtil.TrimBom(bytes, "text/plain; charset=ascii");

        result.ToArray().Should().BeEquivalentTo(bytes);
    }
}
