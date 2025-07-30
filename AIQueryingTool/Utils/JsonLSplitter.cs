namespace TodoApi.Utils;

using System.Text;
using System.Text.Json;
 
public class JsonLSplitter
{
    public static string ExtractValuesOnly(string jsonLine)
    {
        using var doc = JsonDocument.Parse(jsonLine);
        return ExtractValuesRecursive(doc.RootElement);
    }
    private static string ExtractValuesRecursive(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => string.Join(" ", element.EnumerateObject().Select(p => ExtractValuesRecursive(p.Value))),
            JsonValueKind.Array => string.Join(" ", element.EnumerateArray().Select(ExtractValuesRecursive)),
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => element.ToString(),
            _ => string.Empty
        };
    }
 
}