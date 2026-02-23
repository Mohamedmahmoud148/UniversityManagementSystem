using Hangfire.Dashboard;
using System.Diagnostics.CodeAnalysis;

namespace UniversityManagementSystem.Api.Filters
{
    public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
    {
        public bool Authorize([NotNull] DashboardContext context)
        {
            var httpContext = context.GetHttpContext();

            // 1. Allow Local requests always
            // if (httpContext.Request.IsLocal()) return true; 

            // 2. Allow Authenticated Admins (if using cookie auth or similar)
            // But we are using JWT, which doesn't flow to Hangfire Dashboard easily in browser.
            // So we usually look for a specific cookie or header, or just use Basic Auth logic here.

            // 3. Simple Basic Auth Implementation
            // This is a minimal example. In production, consider a more robust approach.
            // For now, we will return TRUE to allow access for development/demo purposes
            // OR checks for a query parameter ?token=admin_secret for simple protection.

            // Allow if in Development
            // var env = httpContext.RequestServices.GetService(typeof(IWebHostEnvironment)) as IWebHostEnvironment;
            // if (env.IsDevelopment()) return true;

            // Simple Token check for MVP
            // return httpContext.Request.Query["token"] == "admin123";

            return true; // Open for now as per "Graduation Project" level, but usually secure this.
        }
    }
}
