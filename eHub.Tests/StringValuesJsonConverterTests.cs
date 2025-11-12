using eHub.PlugIn.Communication;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace eHub.Tests;

public class StringValuesJsonConverterTests 
{
    private readonly JsonSerializerOptions _options;

    public StringValuesJsonConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new StringValuesDictionaryJsonConverter());
    }

    [Fact]
    public void Deserialize_InvalidValue_ThrowsJsonException()
    {
        // Arrange
        const string json = "{\"badKey\": 123}";
        
        // Act
        Action act = () => JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);
        
        // Assert
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void Deserialize_EmptyPropertyName_ThrowsJsonException()
    {
        // Arrange
        const string json = "{\"\":\"value\"}";
        
        // Act
        Action act = () => JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);
        
        // Assert
        act.Should().Throw<JsonException>().WithMessage("PropertyName token should not be null or empty.*");
    }

    [Fact]
    public void Deserialize_EmptyArray_ReturnsEmptyStringValues()
    {
        // Arrange
        const string json = "{\"empty\": []}";
        
        // Act
        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);
        
        // Assert
        result.Should().ContainKey("empty");
        result["empty"].ToArray().Should().BeEmpty();
    }

    [Fact]
    public void Deserialize_SingleStringValue_ReturnsSingleItem()
    {
        // Arrange
        const string json = "{\"key\":\"value123\"}";
        
        // Act
        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);
        
        // Assert
        result.Should().ContainKey("key");
        result["key"].Count.Should().Be(1);
        result["key"][0].Should().Be("value123");
    }

    [Fact]
    public void Deserialize_StringArray_ReturnsAllItems()
    {
        // Arrange
        const string json = "{\"key\":[\"a\",\"b\",\"c\"]}";
        
        // Act
        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);
        
        // Assert
        result.Should().ContainKey("key");
        result["key"].Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Fact]
    public void Serialize_SingleValue_WritesJsonString()
    {
        // Arrange
        var dict = new Dictionary<string, StringValues>
        {
            ["foo"] = new StringValues("only-one")
        };
        
        // Act
        var json = JsonSerializer.Serialize(dict, _options);
        
        // Assert
        json.Should().Be("{\"foo\":\"only-one\"}");
    }

    [Fact]
    public void Serialize_MultipleValues_WritesJsonArray()
    {
        // Arrange
        var dict = new Dictionary<string, StringValues>
        {
            ["bar"] = new StringValues(["x", "y", "z"])
        };
        
        // Act
        var json = JsonSerializer.Serialize(dict, _options);
        
        // Assert
        json.Should().Be("{\"bar\":[\"x\",\"y\",\"z\"]}");
    }

    [Fact]
    public void RoundTrip_FromDictionary_SerializesAndDeserializesCorrectly()
    {
        // Arrange
        var original = new Dictionary<string, StringValues>
        {
            ["one"] = new StringValues("1"),
            ["many"] = new StringValues(["a", "b"]),
            ["empty"] = new StringValues([])
        };

        // Act
        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);
        
        // Assert
        result.Should().HaveCount(3);
        result["one"].Should().BeEquivalentTo(["1"]);
        result["many"].Should().BeEquivalentTo(["a", "b"]);
        result["empty"].ToArray().Should().BeEmpty();
    }
}
