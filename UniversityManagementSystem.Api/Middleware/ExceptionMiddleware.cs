using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Api.Middleware
{
    /// <summary>
    /// Global exception handler middleware.
    /// In Development: includes exception message and stack trace in the response.
    /// In Production:  returns a safe generic message — no internal details exposed.
    /// </summary>
    public class ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        private readonly RequestDelegate _next = next;
        private readonly ILogger<ExceptionMiddleware> _logger = logger;
        private readonly IHostEnvironment _env = env;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var statusCode = exception switch
            {
                UnauthorizedAccessException                        => (int)HttpStatusCode.Unauthorized,
                Core.Exceptions.ForbiddenException                 => (int)HttpStatusCode.Forbidden,
                KeyNotFoundException                               => (int)HttpStatusCode.NotFound,
                Core.Exceptions.ConflictException                  => (int)HttpStatusCode.Conflict,
                Microsoft.EntityFrameworkCore.DbUpdateException    => (int)HttpStatusCode.Conflict,
                ArgumentException                                  => (int)HttpStatusCode.BadRequest,
                InvalidOperationException                          => (int)HttpStatusCode.BadRequest,
                Core.Exceptions.DomainException                    => (int)HttpStatusCode.BadRequest,
                _                                                  => (int)HttpStatusCode.InternalServerError
            };

            context.Response.StatusCode = statusCode;

            // ── Production: return a safe, generic message — NO stack traces ──
            // ── Development: include full detail for debugging ────────────────
            Core.DTOs.ApiResponse<object> response;

            if (_env.IsDevelopment())
            {
                response = new Core.DTOs.ApiResponse<object>
                {
                    Success = false,
                    Message = exception.Message,
                    Errors  = new System.Collections.Generic.List<string>
                    {
                        exception.ToString(),
                        exception.InnerException?.ToString() ?? "No inner exception"
                    }
                };
            }
            else
            {
                // Map status codes to safe user-facing messages
                var safeMessage = statusCode switch
                {
                    (int)HttpStatusCode.Unauthorized => "Authentication required.",
                    (int)HttpStatusCode.Forbidden    => "You do not have permission to perform this action.",
                    (int)HttpStatusCode.NotFound     => "The requested resource was not found.",
                    (int)HttpStatusCode.Conflict when exception is Microsoft.EntityFrameworkCore.DbUpdateException
                                                     => exception.InnerException?.Message ?? exception.Message,
                    (int)HttpStatusCode.Conflict     => exception.Message,
                    (int)HttpStatusCode.BadRequest   => exception.Message,
                    _                                => "An unexpected error occurred. Please try again later."
                };

                response = new Core.DTOs.ApiResponse<object>
                {
                    Success = false,
                    Message = safeMessage,
                    Errors  = new System.Collections.Generic.List<string>()
                };
            }

            var json = JsonSerializer.Serialize(response, _jsonOptions);
            await context.Response.WriteAsync(json);
        }
    }
}
