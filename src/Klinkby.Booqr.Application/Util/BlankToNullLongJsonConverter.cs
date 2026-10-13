using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Util;

/// <summary>
///     Reads a JSON number, numeric string, <c>null</c> or blank string as <c>long?</c>; blank means <c>null</c>.
/// </summary>
/// <remarks>Apply with <c>[property: JsonConverter(typeof(BlankToNullLongJsonConverter))]</c>.</remarks>
public sealed class BlankToNullLongJsonConverter : JsonConverter<long?>
{
    public override bool HandleNull => true;

    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;
            case JsonTokenType.String:
                string? text = reader.GetString();
                if (string.IsNullOrWhiteSpace(text))
                {
                    return null;
                }

                return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out long parsed)
                    ? parsed
                    : throw new JsonException("Phone must be a number.");
            default:
                return reader.GetInt64();
        }
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (value is { } number)
        {
            writer.WriteNumberValue(number);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
