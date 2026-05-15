namespace UniversityManagementSystem.Core.DTOs
{
    /// <summary>
    /// Lightweight doctor picker item for the student complaint form.
    /// Returns only what the dropdown needs: the doctor's profile ID and display name.
    /// </summary>
    public record DoctorOptionDto(string Id, string FullName);
}
