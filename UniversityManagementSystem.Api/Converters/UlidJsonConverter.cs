using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUlid;

namespace UniversityManagementSystem.Api.Converters
{
    /// <summary>
    /// JsonConverterFactory that handles both Ulid (non-nullable) and Ulid? (nullable).
    /// Registering a single factory covers all ULID fields across every DTO —
    /// including when values are stored behind `object` in ApiResponse&lt;object&gt;.
    /// </summary>
    public class UlidJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert)
            => typeToConvert == typeof(Ulid) || typeToConvert == typeof(Ulid?);

        public override JsonConverter? CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        {
            if (typeToConvert == typeof(Ulid?))
                return new NullableUlidJsonConverter();

            return new UlidJsonConverter();
        }
    }

    /// <summary>
    /// Serializes/deserializes a non-nullable Ulid as a plain 26-character ULID string.
    /// </summary>
    public class UlidJsonConverter : JsonConverter<Ulid>
    {
        public override Ulid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var value = reader.GetString();

            if (string.IsNullOrWhiteSpace(value))
                throw new JsonException("ULID value cannot be null or empty.");

            if (!Ulid.TryParse(value, out var ulid))
                throw new JsonException($"'{value}' is not a valid ULID string.");

            return ulid;
        }

        public override void Write(Utf8JsonWriter writer, Ulid value, JsonSerializerOptions options)
            => writer.WriteStringValue(value.ToString());
    }

    /// <summary>
    /// Serializes/deserializes a nullable Ulid? as a plain string or JSON null.
    /// Without this, System.Text.Json falls back to the struct's own properties
    /// (Time, Random) and emits a nested object instead of a string.
    /// </summary>
    public class NullableUlidJsonConverter : JsonConverter<Ulid?>
    {
        public override Ulid? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return null;

            var value = reader.GetString();

            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!Ulid.TryParse(value, out var ulid))
                throw new JsonException($"'{value}' is not a valid ULID string.");

            return ulid;
        }

        public override void Write(Utf8JsonWriter writer, Ulid? value, JsonSerializerOptions options)
        {
            if (value.HasValue)
                writer.WriteStringValue(value.Value.ToString());
            else
                writer.WriteNullValue();
        }
    }
}

