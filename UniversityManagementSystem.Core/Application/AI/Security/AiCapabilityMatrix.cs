namespace UniversityManagementSystem.Core.Application.AI.Security;

/// <summary>
/// Defines which roles are authorised to trigger which AI tools.
/// ChatService validates every tool call against this matrix before execution.
/// </summary>
public static class AiCapabilityMatrix
{
    // ── Role → allowed tool names ────────────────────────────────────────────
    private static readonly Dictionary<string, HashSet<string>> _roleCapabilities = new(
        StringComparer.OrdinalIgnoreCase)
    {
        // Admins can trigger every tool
        ["Admin"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "CreateGeneratedExam",
            "ResolveSubjectOffering",
            "GetStudentOverview",
            "GetStudentGpa",
            "ResolveStudent",
            "ResolveDoctor",
            "ResolveSubject",
            "ResolveOffering",
            "GetOfferingStudents",
            "GetDoctorSubjects",
            "DistributeExams"
        },

        // Doctors can create exams and resolve subject/student context
        ["Doctor"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "CreateGeneratedExam",
            "ResolveSubjectOffering",
            "GetStudentOverview",
            "GetStudentGpa",
            "ResolveStudent",
            "ResolveDoctor",
            "ResolveSubject",
            "ResolveOffering",
            "GetOfferingStudents",
            "GetDoctorSubjects",
            "DistributeExams"
        },

        // Students can only query their own academic overview / GPA (read-only)
        ["Student"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "GetStudentOverview",
            "GetStudentGpa"
        },

        // SuperAdmin inherits everything Admin has
        ["SuperAdmin"] = new(StringComparer.OrdinalIgnoreCase)
        {
            "CreateGeneratedExam",
            "ResolveSubjectOffering",
            "GetStudentOverview",
            "GetStudentGpa",
            "ResolveStudent",
            "ResolveDoctor",
            "ResolveSubject",
            "ResolveOffering",
            "GetOfferingStudents",
            "GetDoctorSubjects",
            "DistributeExams"
        }
    };

    /// <summary>
    /// Returns <c>true</c> if <paramref name="role"/> is permitted to invoke
    /// <paramref name="toolName"/>.  Both parameters are compared case-insensitively.
    /// </summary>
    public static bool IsAllowed(string role, string toolName)
    {
        if (string.IsNullOrWhiteSpace(role) || string.IsNullOrWhiteSpace(toolName))
            return false;

        return _roleCapabilities.TryGetValue(role, out var allowed)
               && allowed.Contains(toolName);
    }
}
