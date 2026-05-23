namespace UniversityManagementSystem.Core.DTOs
{
    public record StudentRiskDto(
        string StudentId,
        string StudentName,
        string SubjectName,
        double AttendancePercent,
        double AverageGrade,
        string RiskLevel,
        string Recommendation);

    public record RiskAnalysisResultDto(
        int StudentsAnalyzed,
        int AtRiskCount,
        int NotificationsSet);

    public record RiskRecommendationRequest(
        string StudentName,
        string SubjectName,
        double AttendancePercent,
        double AverageGrade,
        string RiskLevel);
}
