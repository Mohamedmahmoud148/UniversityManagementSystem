namespace UniversityManagementSystem.Core.Exceptions
{
    public class ForbiddenException : Exception
    {
        public ForbiddenException(string message = "Access denied.") : base(message) { }
    }
}
