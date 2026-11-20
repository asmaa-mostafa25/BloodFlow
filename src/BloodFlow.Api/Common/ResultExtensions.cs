using BloodFlow.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace BloodFlow.Api.Common
{
    /// <summary>
    /// Turns a Result into an HTTP response. Every error becomes a standard
    /// ProblemDetails JSON (RFC 9457) with the same shape, so the frontend can
    /// handle all errors in one place.
    /// </summary>
    internal static class ResultExtensions
    {
        public static IActionResult ToActionResult<T>(this Result<T> result, ControllerBase controller) =>
            result.IsSuccess ? controller.Ok(result.Value) : controller.ToProblem(result.Error!);

        public static IActionResult ToActionResult(this Result result, ControllerBase controller) =>
            result.IsSuccess ? controller.NoContent() : controller.ToProblem(result.Error!);

        public static IActionResult ToProblem(this ControllerBase controller, Error error)
        {
            var status = error.Type switch
            {
                ErrorType.Validation => StatusCodes.Status400BadRequest,
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden => StatusCodes.Status403Forbidden,
                ErrorType.NotFound => StatusCodes.Status404NotFound,
                ErrorType.Conflict => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status500InternalServerError
            };

            var problem = new ProblemDetails
            {
                Status = status,
                Title = error.Message,
                Type = $"https://httpstatuses.io/{status}"
            };
            problem.Extensions["code"] = error.Code;
            problem.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

            return new ObjectResult(problem) { StatusCode = status };
        }
    }
}
