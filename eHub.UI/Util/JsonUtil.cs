using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace eHub.UI.Util;

public static class JsonUtil
{
    public static bool TryParse(string? json, [NotNullWhen(true)] out JsonDocument? jsonDocument)
    {
        if (string.IsNullOrEmpty(json))
        {
            jsonDocument = null;
            return false;
        }

        try
        {
            jsonDocument = JsonDocument.Parse(json);
            return true;
        }
        catch (JsonException)
        {
            jsonDocument = null;
            return false;
        }
    }

    public static bool TryGetElementFromPath(this JsonDocument jsonDocument, string? path, out JsonElement? jsonElement)
    {
        if (string.IsNullOrEmpty(path))
        {
            jsonElement = null;
            return false;
        }

        //  TODO: Implenment a feature similar to XPath from XDocument, or SelectToken from Newtonsoftjson to obtain a JsonElement with a path
        //  Currently System.Text.Json does not provide any support for json path queries

        jsonElement = null;
        return false;
    }
}
