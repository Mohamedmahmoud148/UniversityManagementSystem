using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UniversityManagementSystem.Core.DTOs;

namespace UniversityManagementSystem.Api.Filters
{
    public class ResponseWrapperFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            // Do nothing before execution
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
            if (context.Exception != null) return; // Let exception middleware handle it

            if (context.Result is ObjectResult objectResult)
            {
                // Verify if it's already wrapped or is a problem details (though middleware handles exceptions)
                if (IsApiResponse(objectResult.Value)) return;
                
                // Wrap the response
                var response = new ApiResponse<object>
                {
                    Success = true,
                    Data = objectResult.Value,
                    Message = "Request successful."
                };

                context.Result = new ObjectResult(response)
                {
                    StatusCode = objectResult.StatusCode
                };
            }
            else if (context.Result is EmptyResult)
            {
                 context.Result = new ObjectResult(new ApiResponse<object> { Success = true, Message = "Request successful." })
                 {
                     StatusCode = 200
                 };
            }
             else if (context.Result is StatusCodeResult statusCodeResult && statusCodeResult.StatusCode == 204)
            {
                 // 204 No Content -> Turn into 200 with wrapper? Or leave as is?
                 // Standard practice for 204 is no body. But if we want wrapper, we might need 200 OK.
                 // Let's stick to 200 OK with wrapper for consistency, or keep 204.
                 // Requirement: "Create standard API response wrapper". 
                 // Let's wrap it and return 200.
                 context.Result = new ObjectResult(new ApiResponse<object> { Success = true, Message = "Request successful." })
                 {
                     StatusCode = 200
                 };
            }
        }

        private static bool IsApiResponse(object? value)
        {
            var type = value?.GetType();
            return type is { IsGenericType: true }
                   && type.GetGenericTypeDefinition() == typeof(ApiResponse<>);
        }
    }
}
