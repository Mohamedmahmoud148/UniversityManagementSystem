using System.Collections.Generic;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Infrastructure.Services.Deletion
{
    /// <summary>
    /// Defines how each entity should be deleted and what risk it carries.
    /// Central registry — all deletion decisions are driven from here.
    /// </summary>
    public class EntityDeletionPolicy
    {
        public string     EntityName   { get; init; } = string.Empty;
        public string     FriendlyName { get; init; } = string.Empty;
        public DeleteType DeleteType   { get; init; }
        public DeleteRiskLevel RiskLevel { get; init; }
        public bool       IsHistorical  { get; init; }
        public bool       IsImmutableWhenFinalized { get; init; }
        /// <summary>Typed confirmation phrase the user must type exactly (UPPERCASE).</summary>
        public string?    ConfirmationPhrase { get; init; }
    }

    public static class EntityPolicies
    {
        // ── Friendly display names ───────────────────────────────────────────
        public static readonly Dictionary<string, string> FriendlyNames = new()
        {
            ["University"]           = "University",
            ["College"]              = "College",
            ["Department"]           = "Department",
            ["AcademicYear"]         = "Academic Year",
            ["Semester"]             = "Semester",
            ["Batch"]                = "Batch",
            ["Group"]                = "Group",
            ["Subject"]              = "Subject",
            ["Regulation"]           = "Regulation",
            ["SubjectOffering"]      = "Subject Offering",
            ["SystemUser"]           = "System User",
            ["Student"]              = "Student",
            ["Doctor"]               = "Doctor",
            ["TeachingAssistant"]    = "Teaching Assistant",
            ["Admin"]                = "Admin",
            ["Enrollment"]           = "Enrollment",
            ["StudentGrade"]         = "Student Grade",
            ["Exam"]                 = "Exam",
            ["ExamQuestion"]         = "Exam Question",
            ["ExamSubmission"]       = "Exam Submission",
            ["StudentExamVariant"]   = "Student Exam Variant",
            ["AttendanceSession"]    = "Attendance Session",
            ["StudentAttendance"]    = "Student Attendance",
            ["Material"]             = "Material",
            ["UploadedFile"]         = "Uploaded File",
            ["StudentFile"]          = "Student File",
            ["EnrollmentUpload"]     = "Enrollment Upload",
            ["ScheduleEntry"]        = "Schedule Entry",
            ["Conversation"]         = "Conversation",
            ["ChatMessage"]          = "Chat Message",
            ["AiMemory"]             = "AI Memory",
            ["AiActionLog"]          = "AI Action Log",
            ["AppNotification"]      = "Notification",
            ["RefreshToken"]         = "Refresh Token",
            ["Complaint"]            = "Complaint",
            ["ComplaintAnalysis"]    = "Complaint Analysis",
            ["ComplaintCluster"]     = "Complaint Cluster",
            ["AuditLog"]             = "Audit Log",
            ["RegulationSubject"]    = "Regulation Subject",
            ["SubjectDoctor"]        = "Subject Doctor",
            ["SubjectAssistant"]     = "Subject Assistant",
            ["AcademicYearDepartment"] = "Academic Year Department",
        };

        // ── Policy map ───────────────────────────────────────────────────────
        public static readonly Dictionary<string, EntityDeletionPolicy> All = new()
        {
            // ── CATASTROPHIC ─────────────────────────────────────────────────
            ["University"] = new()
            {
                EntityName   = "University",
                FriendlyName = "University",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Catastrophic,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE UNIVERSITY"
            },
            ["College"] = new()
            {
                EntityName   = "College",
                FriendlyName = "College",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Catastrophic,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE COLLEGE"
            },
            ["Department"] = new()
            {
                EntityName   = "Department",
                FriendlyName = "Department",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Catastrophic,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE DEPARTMENT"
            },
            ["Semester"] = new()
            {
                EntityName   = "Semester",
                FriendlyName = "Semester",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Catastrophic,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE SEMESTER"
            },
            ["SubjectOffering"] = new()
            {
                EntityName   = "SubjectOffering",
                FriendlyName = "Subject Offering",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Catastrophic,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE SUBJECT OFFERING"
            },

            // ── CRITICAL ─────────────────────────────────────────────────────
            ["Student"] = new()
            {
                EntityName   = "Student",
                FriendlyName = "Student",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Critical,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE STUDENT"
            },
            ["SystemUser"] = new()
            {
                EntityName   = "SystemUser",
                FriendlyName = "System User",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Critical,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE USER"
            },
            ["Doctor"] = new()
            {
                EntityName   = "Doctor",
                FriendlyName = "Doctor",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Critical,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE DOCTOR"
            },
            ["Subject"] = new()
            {
                EntityName   = "Subject",
                FriendlyName = "Subject",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Critical,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE SUBJECT"
            },
            ["StudentGrade"] = new()
            {
                EntityName   = "StudentGrade",
                FriendlyName = "Student Grade",
                DeleteType   = DeleteType.ImmutableBlocked,
                RiskLevel    = DeleteRiskLevel.Critical,
                IsHistorical = true,
                IsImmutableWhenFinalized = true
            },
            ["Enrollment"] = new()
            {
                EntityName   = "Enrollment",
                FriendlyName = "Enrollment",
                DeleteType   = DeleteType.Restricted,
                RiskLevel    = DeleteRiskLevel.Critical,
                IsHistorical = true
            },
            ["AuditLog"] = new()
            {
                EntityName   = "AuditLog",
                FriendlyName = "Audit Log",
                DeleteType   = DeleteType.ImmutableBlocked,
                RiskLevel    = DeleteRiskLevel.Critical,
                IsHistorical = true
            },

            // ── HIGH ─────────────────────────────────────────────────────────
            ["AcademicYear"] = new()
            {
                EntityName   = "AcademicYear",
                FriendlyName = "Academic Year",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE ACADEMIC YEAR"
            },
            ["Batch"] = new()
            {
                EntityName   = "Batch",
                FriendlyName = "Batch",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE BATCH"
            },
            ["TeachingAssistant"] = new()
            {
                EntityName   = "TeachingAssistant",
                FriendlyName = "Teaching Assistant",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true
            },
            ["Regulation"] = new()
            {
                EntityName   = "Regulation",
                FriendlyName = "Regulation",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true,
                ConfirmationPhrase = "DELETE REGULATION"
            },
            ["Exam"] = new()
            {
                EntityName   = "Exam",
                FriendlyName = "Exam",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true,
                IsImmutableWhenFinalized = true,
                ConfirmationPhrase = "DELETE EXAM"
            },
            ["ExamSubmission"] = new()
            {
                EntityName   = "ExamSubmission",
                FriendlyName = "Exam Submission",
                DeleteType   = DeleteType.Restricted,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true
            },
            ["AttendanceSession"] = new()
            {
                EntityName   = "AttendanceSession",
                FriendlyName = "Attendance Session",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true
            },
            ["StudentAttendance"] = new()
            {
                EntityName   = "StudentAttendance",
                FriendlyName = "Student Attendance",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.High,
                IsHistorical = true
            },

            // ── MEDIUM ───────────────────────────────────────────────────────
            ["Group"] = new()
            {
                EntityName   = "Group",
                FriendlyName = "Group",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Medium
            },
            ["Complaint"] = new()
            {
                EntityName   = "Complaint",
                FriendlyName = "Complaint",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Medium,
                IsHistorical = true
            },
            ["Material"] = new()
            {
                EntityName   = "Material",
                FriendlyName = "Material",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Medium
            },
            ["UploadedFile"] = new()
            {
                EntityName   = "UploadedFile",
                FriendlyName = "Uploaded File",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Medium
            },
            ["AiMemory"] = new()
            {
                EntityName   = "AiMemory",
                FriendlyName = "AI Memory",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Medium
            },
            ["Conversation"] = new()
            {
                EntityName   = "Conversation",
                FriendlyName = "Conversation",
                DeleteType   = DeleteType.SoftDelete,
                RiskLevel    = DeleteRiskLevel.Medium
            },
            ["ScheduleEntry"] = new()
            {
                EntityName   = "ScheduleEntry",
                FriendlyName = "Schedule Entry",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Medium
            },
            ["EnrollmentUpload"] = new()
            {
                EntityName   = "EnrollmentUpload",
                FriendlyName = "Enrollment Upload",
                DeleteType   = DeleteType.ArchiveOnly,
                RiskLevel    = DeleteRiskLevel.Medium,
                IsHistorical = true
            },

            // ── LOW ──────────────────────────────────────────────────────────
            ["AppNotification"] = new()
            {
                EntityName   = "AppNotification",
                FriendlyName = "Notification",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["RefreshToken"] = new()
            {
                EntityName   = "RefreshToken",
                FriendlyName = "Refresh Token",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["ChatMessage"] = new()
            {
                EntityName   = "ChatMessage",
                FriendlyName = "Chat Message",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["ComplaintAnalysis"] = new()
            {
                EntityName   = "ComplaintAnalysis",
                FriendlyName = "Complaint Analysis",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["ComplaintCluster"] = new()
            {
                EntityName   = "ComplaintCluster",
                FriendlyName = "Complaint Cluster",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["ExamQuestion"] = new()
            {
                EntityName   = "ExamQuestion",
                FriendlyName = "Exam Question",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["StudentExamVariant"] = new()
            {
                EntityName   = "StudentExamVariant",
                FriendlyName = "Exam Variant",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["StudentFile"] = new()
            {
                EntityName   = "StudentFile",
                FriendlyName = "Student File",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["AiActionLog"] = new()
            {
                EntityName   = "AiActionLog",
                FriendlyName = "AI Action Log",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["SubjectDoctor"] = new()
            {
                EntityName   = "SubjectDoctor",
                FriendlyName = "Subject-Doctor Assignment",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["SubjectAssistant"] = new()
            {
                EntityName   = "SubjectAssistant",
                FriendlyName = "Subject-TA Assignment",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["RegulationSubject"] = new()
            {
                EntityName   = "RegulationSubject",
                FriendlyName = "Regulation Subject",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
            ["AcademicYearDepartment"] = new()
            {
                EntityName   = "AcademicYearDepartment",
                FriendlyName = "Academic Year Department",
                DeleteType   = DeleteType.HardDelete,
                RiskLevel    = DeleteRiskLevel.Low
            },
        };

        public static EntityDeletionPolicy GetPolicy(string entityName)
        {
            if (All.TryGetValue(entityName, out var policy)) return policy;
            return new EntityDeletionPolicy
            {
                EntityName   = entityName,
                FriendlyName = entityName,
                DeleteType   = DeleteType.Restricted,
                RiskLevel    = DeleteRiskLevel.High
            };
        }
    }
}
