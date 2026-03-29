using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUlid;

namespace UniversityManagementSystem.Api.Converters
{
    /// <summary>
    /// Allows System.Text.Json to deserialize/serialize ULID as a plain string.
    /// Fixes: "The dto field is required" when the body contains Ulid-typed properties
    /// sent from the client as strings (e.g. "01KMXCAWPP16MDYJKA7W7GKH8J").
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
        {
            writer.WriteStringValue(value.ToString());
        }
    }
}
