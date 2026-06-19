using System.Threading.Tasks;

namespace UniversityManagementSystem.Core.Interfaces
{
    public interface IUniversityEmailGenerator
    {
        /// <summary>
        /// Generates a unique university email for a new student.
        /// Format: {normalizedname}.student{n}@benisuefnationaluniversity.edu
        /// Increments n until a unique email is found.
        /// </summary>
        Task<string> GenerateStudentEmailAsync(string fullName);
    }
}
