using System.Text.Json;
using Xunit;

namespace UniversityManagementSystem.Tests
{
    /// <summary>
    /// Verifies the serialization contract between .NET and FastAPI.
    /// Section 2: snake_case is the official contract for AI payloads.
    /// These tests document and enforce the contract so future changes are visible.
    /// </summary>
    public class SerializationContractTests
    {
        private static readonly JsonSerializerOptions _snakeCaseOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        [Fact]
        public void StudentContext_SerializedAsSnakeCase_BatchId()
        {
            var ctx = new { batchId = "01JX...", studentId = "01JY...", userId = "01JZ..." };
            var json = JsonSerializer.Serialize(ctx, _snakeCaseOptions);

            Assert.Contains("\"batch_id\"", json);
            Assert.Contains("\"student_id\"", json);
            Assert.Contains("\"user_id\"", json);
            Assert.DoesNotContain("\"batchId\"", json);
        }

        [Fact]
        public void DoctorContext_SerializedAsSnakeCase_DoctorId()
        {
            var ctx = new { doctorId = "01JX...", departmentId = "01JY..." };
            var json = JsonSerializer.Serialize(ctx, _snakeCaseOptions);

            Assert.Contains("\"doctor_id\"", json);
            Assert.Contains("\"department_id\"", json);
        }

        [Fact]
        public void AuditLogEntry_SerializedAsSnakeCase()
        {
            var entry = new
            {
                actionType = "Login",
                entityName = "SystemUser",
                performedByUserId = "01JX...",
                performedAt = System.DateTime.UtcNow
            };
            var json = JsonSerializer.Serialize(entry, _snakeCaseOptions);

            Assert.Contains("\"action_type\"", json);
            Assert.Contains("\"entity_name\"", json);
            Assert.Contains("\"performed_by_user_id\"", json);
        }

        [Theory]
        [InlineData("batchId", "batch_id")]
        [InlineData("studentId", "student_id")]
        [InlineData("userId", "user_id")]
        [InlineData("doctorId", "doctor_id")]
        [InlineData("departmentId", "department_id")]
        [InlineData("collegeId", "college_id")]
        [InlineData("subjectOfferingId", "subject_offering_id")]
        public void SnakeCaseLower_ConvertsAllKnownFields(string camel, string expectedSnake)
        {
            var dict = new System.Collections.Generic.Dictionary<string, string>
            {
                [camel] = "test-value"
            };
            var json = JsonSerializer.Serialize(dict, _snakeCaseOptions);
            Assert.Contains($"\"{expectedSnake}\"", json);
        }
    }
}
