using System;
using System.Collections.Generic;

namespace UniversityManagementSystem.Core.DTOs
{
    public record RecordProctoringEventDto(
        string ExamSubmissionId,
        string EventType,
        string? Details);
    // EventType: "tab_switch", "fullscreen_exit", "copy_attempt", "right_click", "focus_loss"

    public record ProctoringEventDto(string Type, DateTime Timestamp, string? Details);

    public record ProctoringReportDto(
        string SubmissionId,
        string StudentName,
        int TabSwitches,
        int FullscreenExits,
        int SuspiciousCount,
        string Status,
        List<ProctoringEventDto> Events);

    public record FlagSubmissionDto(string SubmissionId, string Note);

    public record ProctoringStudentSummaryDto
    {
        public string SubmissionId        { get; init; } = string.Empty;
        public string StudentName         { get; init; } = string.Empty;
        public string StudentCode         { get; init; } = string.Empty;
        public int    TabSwitches         { get; init; }
        public int    FullscreenExits     { get; init; }
        public int    SuspiciousCount     { get; init; }
        public string Status              { get; init; } = string.Empty;
        public string? DoctorNote         { get; init; }
    }
}
