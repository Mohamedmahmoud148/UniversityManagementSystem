using System.Collections.Generic;
using System.Threading.Tasks;
using NUlid;
using UniversityManagementSystem.Core.DTOs.TeachingIntelligence;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface ITeachingIntelligenceService
    {
        // ── Doctor overview ───────────────────────────────────────────────
        /// Get all subject offerings this doctor teaches (with summary stats)
        Task<List<DoctorOfferingSummaryDto>> GetDoctorOfferingsAsync(Ulid doctorUserId);

        /// Full teaching dashboard: all offerings + at-risk + weak topics + alerts
        Task<TeachingDashboardDto> GetDashboardAsync(Ulid doctorUserId);

        // ── Class analytics ───────────────────────────────────────────────
        /// Detailed analytics for a single subject offering
        Task<ClassIntelligenceDto> GetClassIntelligenceAsync(
            Ulid subjectOfferingId, Ulid doctorUserId, TeachingQueryFilter filter);

        /// Compare performance across multiple groups/batches for one subject
        Task<List<ClassComparisonDto>> GetClassComparisonAsync(
            string subjectName, Ulid doctorUserId);

        // ── Student analytics ─────────────────────────────────────────────
        /// Get paginated student analytics for an offering
        Task<List<StudentIntelligenceDto>> GetStudentsAsync(
            Ulid subjectOfferingId, Ulid doctorUserId, TeachingQueryFilter filter);

        /// Get analytics for a single student in a specific offering
        Task<StudentIntelligenceDto?> GetStudentAnalyticsAsync(
            Ulid studentId, Ulid subjectOfferingId, Ulid doctorUserId);

        /// Get at-risk students across ALL of this doctor's offerings
        Task<List<StudentIntelligenceDto>> GetAtRiskStudentsAsync(
            Ulid doctorUserId, string? minRiskLevel = "medium");

        /// Get most improved students (positive grade/attendance trend)
        Task<List<StudentIntelligenceDto>> GetMostImprovedAsync(
            Ulid subjectOfferingId, Ulid doctorUserId, int limit = 10);

        // ── Topic analytics ───────────────────────────────────────────────
        Task<TopicAnalyticsDto> GetTopicAnalyticsAsync(
            Ulid subjectOfferingId, Ulid doctorUserId);

        // ── Alerts ────────────────────────────────────────────────────────
        Task<List<TeachingAlertDto>> GetAlertsAsync(
            Ulid doctorUserId, bool unreadOnly = false);
        Task MarkAlertReadAsync(Ulid alertId, Ulid doctorUserId);

        // ── AI Insights ───────────────────────────────────────────────────
        Task<List<TeachingInsightDto>> GetAiInsightsAsync(
            Ulid doctorUserId, string? offeringId = null);

        // ── Excel Export ──────────────────────────────────────────────────
        /// Returns structured data ready for the frontend to render as Excel
        Task<ExcelExportMetaDto> GetStudentExportDataAsync(
            Ulid subjectOfferingId, Ulid doctorUserId);

        /// Export all students for a specific batch across doctor's offerings
        Task<ExcelExportMetaDto> GetBatchExportDataAsync(
            Ulid batchId, Ulid doctorUserId, Ulid? subjectOfferingId = null);

        /// Generate a grades-entry template .xlsx — columns match ImportGradesFromExcelAsync
        /// so the doctor can fill in grades and re-upload without errors.
        Task<(byte[] bytes, string fileName)> GenerateGradesTemplateAsync(
            Ulid subjectOfferingId, Ulid doctorUserId);

        // ── Snapshot management (called by background job) ────────────────
        Task RefreshSnapshotAsync(Ulid subjectOfferingId);
        Task RefreshAllDoctorSnapshotsAsync(Ulid doctorUserId);
    }
}
