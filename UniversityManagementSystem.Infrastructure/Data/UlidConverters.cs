using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using NUlid;

namespace UniversityManagementSystem.Infrastructure.Data
{
    /// <summary>
    /// EF Core value converter: Ulid (non-nullable) ↔ string (VARCHAR 26).
    /// </summary>
    public class UlidToStringConverter : ValueConverter<Ulid, string>
    {
        public UlidToStringConverter()
            : base(
                v => v.ToString(),
                v => Ulid.Parse(v))
        {
        }
    }

    /// <summary>
    /// EF Core value converter: Ulid? (nullable) ↔ string? (VARCHAR 26, NULL-safe).
    /// </summary>
    public class NullableUlidToStringConverter : ValueConverter<Ulid?, string?>
    {
        public NullableUlidToStringConverter()
            : base(
                v => v.HasValue ? v.Value.ToString() : null,
                v => v == null ? (Ulid?)null : Ulid.Parse(v))
        {
        }
    }
}
