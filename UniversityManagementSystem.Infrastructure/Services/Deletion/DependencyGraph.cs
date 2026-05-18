using System.Collections.Generic;

namespace UniversityManagementSystem.Infrastructure.Services.Deletion
{
    /// <summary>
    /// Declares the child-dependency map for every entity.
    /// Key   = parent entity name
    /// Value = list of child relationships it owns
    /// </summary>
    public static class DependencyGraph
    {
        public class ChildRelation
        {
            public string ChildEntity    { get; init; } = string.Empty;
            public string FriendlyName   { get; init; } = string.Empty;
            /// <summary>EF delete behavior for display purposes only.</summary>
            public string DeleteBehavior { get; init; } = "Restrict";
            public bool   IsHistorical   { get; init; }
            public bool   IsBlocking     { get; init; } = true;
        }

        public static readonly Dictionary<string, List<ChildRelation>> Children = new()
        {
            ["University"] = new()
            {
                new() { ChildEntity = "College",   FriendlyName = "Colleges",   DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "Student",   FriendlyName = "Students",   DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
            },
            ["College"] = new()
            {
                new() { ChildEntity = "Department",  FriendlyName = "Departments",  DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "AcademicYear", FriendlyName = "Academic Years", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "Subject",     FriendlyName = "Subjects",     DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "Student",     FriendlyName = "Students",     DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
            },
            ["Department"] = new()
            {
                new() { ChildEntity = "Batch",             FriendlyName = "Batches",          DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "Doctor",            FriendlyName = "Doctors",           DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "TeachingAssistant", FriendlyName = "Teaching Assistants", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "Subject",           FriendlyName = "Subjects",          DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "Regulation",        FriendlyName = "Regulations",       DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = false },
                new() { ChildEntity = "AcademicYearDepartment", FriendlyName = "Year Mappings", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
            },
            ["AcademicYear"] = new()
            {
                new() { ChildEntity = "Semester",              FriendlyName = "Semesters",        DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "AcademicYearDepartment", FriendlyName = "Dept Mappings",   DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
            },
            ["Semester"] = new()
            {
                new() { ChildEntity = "SubjectOffering", FriendlyName = "Subject Offerings", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
            },
            ["Batch"] = new()
            {
                new() { ChildEntity = "Group",          FriendlyName = "Groups",         DeleteBehavior = "Restrict", IsHistorical = false, IsBlocking = true },
                new() { ChildEntity = "Student",        FriendlyName = "Students",       DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "ScheduleEntry",  FriendlyName = "Schedule Entries", DeleteBehavior = "Restrict", IsHistorical = false, IsBlocking = false },
            },
            ["Group"] = new()
            {
                new() { ChildEntity = "Student",        FriendlyName = "Students",       DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "SubjectOffering", FriendlyName = "Subject Offerings", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "ScheduleEntry",  FriendlyName = "Schedule Entries", DeleteBehavior = "Restrict", IsHistorical = false, IsBlocking = false },
            },
            ["Subject"] = new()
            {
                new() { ChildEntity = "SubjectOffering",  FriendlyName = "Subject Offerings", DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "SubjectDoctor",    FriendlyName = "Doctor Assignments", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "SubjectAssistant", FriendlyName = "TA Assignments",    DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "RegulationSubject", FriendlyName = "Regulation Links", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "AttendanceSession", FriendlyName = "Attendance Sessions", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
            },
            ["Regulation"] = new()
            {
                new() { ChildEntity = "RegulationSubject", FriendlyName = "Subject Links", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
            },
            ["SubjectOffering"] = new()
            {
                new() { ChildEntity = "Enrollment",        FriendlyName = "Enrollments",     DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "Exam",              FriendlyName = "Exams",           DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "StudentGrade",      FriendlyName = "Student Grades",  DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "Material",          FriendlyName = "Materials",       DeleteBehavior = "SoftDelete", IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "ScheduleEntry",     FriendlyName = "Schedule Entries", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
            },
            ["Doctor"] = new()
            {
                new() { ChildEntity = "SubjectOffering",  FriendlyName = "Subject Offerings", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "SubjectDoctor",    FriendlyName = "Subject Assignments", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "AttendanceSession", FriendlyName = "Attendance Sessions", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "Material",         FriendlyName = "Materials",          DeleteBehavior = "Restrict", IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "Exam",             FriendlyName = "Created Exams",      DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
            },
            ["TeachingAssistant"] = new()
            {
                new() { ChildEntity = "SubjectAssistant",  FriendlyName = "Subject Assignments", DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "AttendanceSession", FriendlyName = "Attendance Sessions", DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
            },
            ["Student"] = new()
            {
                new() { ChildEntity = "Enrollment",        FriendlyName = "Enrollments",        DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "StudentGrade",      FriendlyName = "Grades",             DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "ExamSubmission",    FriendlyName = "Exam Submissions",   DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "StudentAttendance", FriendlyName = "Attendance Records", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
                new() { ChildEntity = "StudentExamVariant", FriendlyName = "Exam Variants",     DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "StudentFile",       FriendlyName = "Student Files",      DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "Complaint",         FriendlyName = "Complaints",         DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = false },
            },
            ["SystemUser"] = new()
            {
                new() { ChildEntity = "Conversation",     FriendlyName = "Conversations",    DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "AiMemory",         FriendlyName = "AI Memories",      DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "AppNotification",  FriendlyName = "Notifications",    DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "RefreshToken",     FriendlyName = "Refresh Tokens",   DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
            },
            ["Exam"] = new()
            {
                new() { ChildEntity = "ExamQuestion",       FriendlyName = "Questions",         DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
                new() { ChildEntity = "ExamSubmission",     FriendlyName = "Submissions",        DeleteBehavior = "Restrict", IsHistorical = true,  IsBlocking = true },
                new() { ChildEntity = "StudentExamVariant", FriendlyName = "Student Variants",   DeleteBehavior = "Cascade",  IsHistorical = false, IsBlocking = false },
            },
            ["Complaint"] = new()
            {
                new() { ChildEntity = "ComplaintAnalysis", FriendlyName = "AI Analysis", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
            },
            ["Conversation"] = new()
            {
                new() { ChildEntity = "ChatMessage", FriendlyName = "Messages", DeleteBehavior = "Cascade", IsHistorical = false, IsBlocking = false },
            },
            ["AttendanceSession"] = new()
            {
                new() { ChildEntity = "StudentAttendance", FriendlyName = "Attendance Records", DeleteBehavior = "Restrict", IsHistorical = true, IsBlocking = true },
            },
        };

        /// <summary>Leaf-first deletion order for complete teardown of the schema.</summary>
        public static readonly List<string> SafeDeletionOrder = new()
        {
            "ComplaintAnalysis",
            "ComplaintCluster",
            "AiActionLog",
            "AiMemory",
            "ChatMessage",
            "Conversation",
            "AuditLog",
            "AppNotification",
            "RefreshToken",
            "StudentExamVariant",
            "ExamSubmission",
            "ExamQuestion",
            "Exam",
            "StudentGrade",
            "StudentAttendance",
            "AttendanceSession",
            "Complaint",
            "StudentFile",
            "Material",
            "EnrollmentUpload",
            "UploadedFile",
            "ScheduleEntry",
            "Enrollment",
            "SubjectOffering",
            "SubjectDoctor",
            "SubjectAssistant",
            "RegulationSubject",
            "AcademicYearDepartment",
            "Student",
            "Doctor",
            "TeachingAssistant",
            "Admin",
            "SystemUser",
            "Group",
            "Batch",
            "Subject",
            "Regulation",
            "Semester",
            "AcademicYear",
            "Department",
            "College",
            "University",
        };
    }
}
