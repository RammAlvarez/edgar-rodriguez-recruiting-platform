using System.Text.Json;

namespace Platform.Core.Http;

/// <summary>Enums in query strings use the wire spelling (snake_case, like the JSON bodies and the Postgres labels, D55).</summary>
public static class WireEnum
{
    /// <summary>Null or empty → <c>null</c>; an unknown value → <c>false</c> (the route answers 400).</summary>
    public static bool TryParse<T>(string? value, out T? result) where T : struct, Enum
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
            return true;
        foreach (var e in Enum.GetValues<T>())
        {
            if (JsonNamingPolicy.SnakeCaseLower.ConvertName(e.ToString()) == value.Trim())
            {
                result = e;
                return true;
            }
        }
        return false;
    }
}
