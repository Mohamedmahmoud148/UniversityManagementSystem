namespace UniversityManagementSystem.Core.Constants
{
    public static class AuditActions
    {
        // Generic CRUD
        public const string Create     = "Create";
        public const string Update     = "Update";
        public const string Delete     = "Delete";
        public const string SoftDelete = "SoftDelete";
        public const string Restore    = "Restore";

        // Authentication
        public const string Login          = "Login";
        public const string Logout         = "Logout";
        public const string FailedLogin    = "FailedLogin";
        public const string RefreshToken   = "RefreshToken";
        public const string ChangePassword = "ChangePassword";

        // Grades
        public const string ImportGrades      = "ImportGrades";
        public const string UpdateGrade       = "UpdateGrade";
        public const string FinalizeGrades    = "FinalizeGrades";
        public const string RecalculateGrades = "RecalculateGrades";
        public const string ExportGrades      = "ExportGrades";

        // Students
        public const string CreateStudent = "CreateStudent";
        public const string UpdateStudent = "UpdateStudent";
        public const string DeleteStudent = "DeleteStudent";
        public const string RegisterCourse = "RegisterCourse";
        public const string DropCourse    = "DropCourse";

        // Exams
        public const string GenerateExam = "GenerateExam";
        public const string PublishExam  = "PublishExam";
        public const string UpdateExam   = "UpdateExam";
        public const string DeleteExam   = "DeleteExam";

        // Notifications
        public const string CreateNotification = "CreateNotification";
        public const string SendNotification   = "SendNotification";
        public const string DeleteNotification = "DeleteNotification";

        // Academic
        public const string CreateSemester       = "CreateSemester";
        public const string CreateSubjectOffering = "CreateSubjectOffering";
        public const string EnrollStudent        = "EnrollStudent";
        public const string DropEnrollment       = "DropEnrollment";
        public const string Enroll               = "Enroll"; // legacy alias

        // Security
        public const string UnauthorizedAccess  = "UnauthorizedAccess";
        public const string ForbiddenAction     = "ForbiddenAction";
        public const string RoleChanged         = "RoleChanged";
        public const string SuspiciousActivity  = "SuspiciousActivity";
    }
}
