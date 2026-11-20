using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace BloodFlow.Api.Common
{
    /// <summary>
    /// Last safety net for unexpected exceptions. The client gets a clean ProblemDetails
    /// (never a stack trace); the full error goes to the server log with the trace id.
    /// </summary>
    internal sealed class GlobalExceptionHandler : IExceptionHandler
    {
        private readonly ILogger<GlobalExceptionHandler> _logger;
        private readonly IProblemDetailsService _problemDetailsService;

        public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService)
        {
            _logger = logger;
            _problemDetailsService = problemDetailsService;
        }

        public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
        {
            var (status, title, code) = exception switch
            {
                DbUpdateConcurrencyException => (StatusCodes.Status409Conflict,
                    "The record was changed by someone else. Reload and try again.", "Concurrency"),

                // 2601/2627 = unique index or unique constraint violation (e.g. two requests at the same moment).
                DbUpdateException { InnerException: SqlException { Number: 2601 or 2627 } } => (StatusCodes.Status409Conflict,
                    "A record with the same unique value already exists.", "Duplicate"),

                _ => (StatusCodes.Status500InternalServerError,
                    "An unexpected error occurred.", "ServerError")
            };

            if (status == StatusCodes.Status500InternalServerError)
            {
                _logger.LogError(exception, "Unhandled exception. TraceId: {TraceId}", httpContext.TraceIdentifier);
            }
            else
            {
                _logger.LogWarning(exception, "Database conflict. TraceId: {TraceId}", httpContext.TraceIdentifier);
            }

            httpContext.Response.StatusCode = status;

            var problem = new ProblemDetails
            {
                Status = status,
                Title = title,
                Type = $"https://httpstatuses.io/{status}"
            };
            problem.Extensions["code"] = code;
            problem.Extensions["traceId"] = httpContext.TraceIdentifier;

            return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problem,
                Exception = exception
            });
        }
    }
}
