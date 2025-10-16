using eHub.PlugIn.Communication;
using FluentAssertions;
using Microsoft.Extensions.Primitives;
using System.Text.Json;

namespace eHub.Tests;

[TestClass]
public class StringValuesJsonConverterTests
{
    private JsonSerializerOptions _options = default!;

    [TestInitialize]
    public void Setup()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new StringValuesDictionaryJsonConverter());
    }

    [TestMethod]
    public void Deserialize_InvalidValue_ThrowsJsonException()
    {
        var json = "{\"badKey\": 123}";

        Action act = () => JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);

        act.Should().Throw<JsonException>();
    }

    [TestMethod]
    public void Deserialize_EmptyPropertyName_ThrowsJsonException()
    {
        var json = "{\"\":\"value\"}";

        Action act = () => JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);

        act.Should().Throw<JsonException>().WithMessage("PropertyName token should not be null or empty.*");
    }

    [TestMethod]
    public void Deserialize_EmptyArray_ReturnsEmptyStringValues()
    {
        var json = "{\"empty\": []}";

        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);

        result.Should().ContainKey("empty");
        result["empty"].ToArray().Should().BeEmpty();
    }

    [TestMethod]
    public void Deserialize_SingleStringValue_ReturnsSingleItem()
    {
        var json = "{\"key\":\"value123\"}";

        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);

        result.Should().ContainKey("key");
        result["key"].Count.Should().Be(1);
        result["key"][0].Should().Be("value123");
    }

    [TestMethod]
    public void Deserialize_StringArray_ReturnsAllItems()
    {
        var json = "{\"key\":[\"a\",\"b\",\"c\"]}";

        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);

        result.Should().ContainKey("key");
        result["key"].Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [TestMethod]
    public void Serialize_SingleValue_WritesJsonString()
    {
        var dict = new Dictionary<string, StringValues>
        {
            ["foo"] = new StringValues("only-one")
        };

        var json = JsonSerializer.Serialize(dict, _options);

        json.Should().Be("{\"foo\":\"only-one\"}");
    }

    [TestMethod]
    public void Serialize_MultipleValues_WritesJsonArray()
    {
        var dict = new Dictionary<string, StringValues>
        {
            ["bar"] = new StringValues(["x", "y", "z"])
        };

        var json = JsonSerializer.Serialize(dict, _options);

        json.Should().Be("{\"bar\":[\"x\",\"y\",\"z\"]}");
    }

    [TestMethod]
    public void RoundTrip_FromDictionary_SerializesAndDeserializesCorrectly()
    {
        var original = new Dictionary<string, StringValues>
        {
            ["one"] = new StringValues("1"),
            ["many"] = new StringValues(["a", "b"]),
            ["empty"] = new StringValues([])
        };

        var json = JsonSerializer.Serialize(original, _options);
        var result = JsonSerializer.Deserialize<Dictionary<string, StringValues>>(json, _options);

        result.Should().HaveCount(3);
        result["one"].Should().BeEquivalentTo(["1"]);
        result["many"].Should().BeEquivalentTo(["a", "b"]);
        result["empty"].ToArray().Should().BeEmpty();
    }
}
