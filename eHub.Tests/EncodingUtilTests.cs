using eHub.PlugIn.Communication;
using FluentAssertions;
using System.Text;

namespace eHub.Tests;

public class EncodingUtilTests
{
    private const string TextUtf8 = "Sömé UTF8 tëxt.";
    private const string TextAscii = "Plain ASCII text.";

    [Fact]
    public void TrimBom_WithUtf8Bom_RemovesPreamble()
    {
        // Arrange
        var bom = Encoding.UTF8.GetPreamble();
        var content = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(content);

        // Act
        var result = EncodingUtil.TrimBom(memory, "application/json; charset=utf-8");
        var decoded = Encoding.UTF8.GetString(result.Span);

        // Assert
        decoded.Should().Be(TextUtf8);
    }

    [Fact]
    public void TrimBom_WithoutBom_ReturnsOriginal()
    {
        // Arrange
        var content = Encoding.UTF8.GetBytes(TextUtf8);

        // Act
        var result = EncodingUtil.TrimBom(content, "application/json; charset=utf-8");

        // Assert
        result.ToArray().Should().BeEquivalentTo(content);
    }

    [Fact]
    public void TrimBom_WithNullContentType_DefaultsToUtf8()
    {
        // Arrange
        var bom = Encoding.UTF8.GetPreamble();
        var contentBytes = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(contentBytes);

        // Act
        var result = EncodingUtil.TrimBom(memory, null);
        var decoded = Encoding.UTF8.GetString(result.Span);

        // Assert
        decoded.Should().Be(TextUtf8);
    }

    [Fact]
    public void TrimBom_WithMissingCharset_DefaultsToUtf8()
    {
        // Arrange
        var bom = Encoding.UTF8.GetPreamble();
        var contentBytes = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(contentBytes);

        // Act
        var result = EncodingUtil.TrimBom(memory, "application/json");
        var decoded = Encoding.UTF8.GetString(result.Span);

        // Assert
        decoded.Should().Be(TextUtf8);
    }

    [Fact]
    public void TrimBom_WithInvalidContentType_FallbacksToUtf8()
    {
        // Arrange
        var bom = Encoding.UTF8.GetPreamble();
        var contentBytes = bom.Concat(Encoding.UTF8.GetBytes(TextUtf8)).ToArray();
        var memory = new ReadOnlyMemory<byte>(contentBytes);

        // Act
        var result = EncodingUtil.TrimBom(memory, "invalid/type; charset=invalid");
        var decoded = Encoding.UTF8.GetString(result.Span);

        // Assert
        decoded.Should().Be(TextUtf8);
    }

    [Fact]
    public void TrimBom_WithAsciiEncoding_ReturnsOriginal()
    {
        // Arrange
        var bytes = Encoding.ASCII.GetBytes(TextAscii);

        // Act
        var result = EncodingUtil.TrimBom(bytes, "text/plain; charset=ascii");

        // Assert
        result.ToArray().Should().BeEquivalentTo(bytes);
    }
}
