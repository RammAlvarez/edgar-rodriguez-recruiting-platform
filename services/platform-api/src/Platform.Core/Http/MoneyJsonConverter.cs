using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Platform.Core.Http;

/// <summary>
/// Every <see cref="decimal"/> travels as a decimal string ("1234.56"), never a JSON number, so no client parses money
/// into a binary float (DATA_MODEL conventions, PLAN D46). JSON numbers are rejected on input.
/// </summary>
public sealed partial class DecimalStringJsonConverter : JsonConverter<decimal>
{
    public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException("Decimal values must be sent as strings.");
        var text = reader.GetString()!;
        if (!DecimalPattern().IsMatch(text))
            throw new JsonException($"'{text}' is not a decimal string.");
        return decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));

    [GeneratedRegex(@"^-?\d{1,18}(\.\d{1,28})?$")]
    private static partial Regex DecimalPattern();
}
