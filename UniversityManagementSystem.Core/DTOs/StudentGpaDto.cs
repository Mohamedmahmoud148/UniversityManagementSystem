namespace UniversityManagementSystem.Core.DTOs
{
    public class StudentGpaDto
    {
        public int StudentId { get; set; } // Optional: useful contextual info
        public string StudentName { get; set; } = string.Empty; // Optional: useful for Admin view
        public double GPA { get; set; }
        public int TotalCreditHours { get; set; }
        public int TotalSubjects { get; set; }
    }
}
