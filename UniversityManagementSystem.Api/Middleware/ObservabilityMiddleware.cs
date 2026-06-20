using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace UniversityManagementSystem.Api.Middleware
{
    /// <summary>
    /// Structured observability middleware — Section 9.
    ///
    /// Adds to every request:
    ///   - CorrelationId (X-Correlation-Id header or generated)
    ///   - RequestId
    ///   - UserId (from JWT claims)
    ///   - ExecutionTime
    ///
    /// Log format (structured, compatible with Serilog/Seq/DataDog):
    ///   RequestId=xxx CorrelationId=xxx UserId=xxx Path=xxx Duration=Nms StatusCode=200
    /// </summary>
    public class ObservabilityMiddleware(
        RequestDelegate next,
        ILogger<ObservabilityMiddleware> logger)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = context.Request.Headers["X-Correlation-Id"].ToString();
            if (string.IsNullOrWhiteSpace(correlationId))
                correlationId = Guid.NewGuid().ToString("N")[..16];

            var requestId = context.TraceIdentifier;

            // Inject correlation ID into response so clients can trace
            context.Response.Headers["X-Correlation-Id"] = correlationId;
            context.Response.Headers["X-Request-Id"]     = requestId;

            // Make available to other middleware/services via Items
            context.Items["CorrelationId"] = correlationId;
            context.Items["RequestId"]     = requestId;

            var sw = Stopwatch.StartNew();

            using var scope = logger.BeginScope(new
            {
                CorrelationId = correlationId,
                RequestId     = requestId,
            });

            try
            {
                await next(context);
            }
            finally
            {
                sw.Stop();
                var userId = context.User?.FindFirst("sub")?.Value
                          ?? context.User?.FindFirst("nameid")?.Value
                          ?? "anonymous";

                logger.LogInformation(
                    "HTTP {Method} {Path} → {StatusCode} | Duration={DurationMs}ms | " +
                    "CorrelationId={CorrelationId} RequestId={RequestId} UserId={UserId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.Response.StatusCode,
                    sw.ElapsedMilliseconds,
                    correlationId,
                    requestId,
                    userId);
            }
        }
    }
}
