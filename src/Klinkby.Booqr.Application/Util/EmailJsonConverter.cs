using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Klinkby.Booqr.Application.Util;

/// <summary>
///     Normalizes an email address (trimmed, lower-case) as it is deserialized, so validation attributes and
///     user lookups see one canonical form regardless of client auto-capitalization or stray whitespace.
/// </summary>
/// <remarks>Apply with <c>[property: JsonConverter(typeof(EmailJsonConverter))]</c>.</remarks>
public sealed class EmailJsonConverter : JsonConverter<string>
{
    [SuppressMessage("Globalization", "CA1308:Normalize strings to uppercase",
        Justification = "Email addresses are conventionally stored and compared in lower case")]
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString()?.Trim().ToLowerInvariant();

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value);
    }
}
