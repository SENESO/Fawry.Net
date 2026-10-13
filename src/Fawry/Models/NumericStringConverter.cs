using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Fawry.Models
{
    /// <summary>
    /// Reads a JSON string or number into a <see cref="string"/>.
    /// Fawry is inconsistent across endpoints: some responses send status
    /// codes as <c>"200"</c>, others as <c>200</c>. Without this converter,
    /// a numeric status code would throw during deserialization.
    /// </summary>
    internal sealed class NumericStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Number:
                    // Status codes are integral; GetRawText() only exists in STJ 9+.
                    return reader.GetInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException($"Cannot convert {reader.TokenType} to string.");
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            if (value == null)
                writer.WriteNullValue();
            else
                writer.WriteStringValue(value);
        }
    }
}
