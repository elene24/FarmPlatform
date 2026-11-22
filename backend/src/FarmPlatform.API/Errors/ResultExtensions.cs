using FarmPlatform.Application.Common.Results;
using Microsoft.AspNetCore.Mvc;

namespace FarmPlatform.Api.Errors;

/// <summary>
/// Converts use-case results into HTTP responses. Every error body is an RFC 9457 problem with a top-level
/// "code" and, for field errors, an "errors" map of field -> [codes] that the frontend shows next to inputs.
/// </summary>
public static class ResultExtensions
{
    public static ActionResult ToProblem(this ControllerBase controller, Result result)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new InvalidOperationException("Cannot create a problem response from a successful result.");
        }

        var status = result.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            ErrorKind.Locked => StatusCodes.Status423Locked,
            _ => StatusCodes.Status400BadRequest,
        };

        var fieldErrors = result.Errors
            .Where(e => e.Field is not null)
            .GroupBy(e => e.Field!)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Code).Distinct().ToArray());

        var firstCode = result.Errors.Count > 0 ? result.Errors[0].Code : "error";
        var problem = new ValidationProblemDetails(fieldErrors)
        {
            Status = status,
            Title = firstCode,
        };
        problem.Extensions["code"] = result.Kind == ErrorKind.Validation && result.Errors.Count > 1
            ? "validation.failed"
            : firstCode;

        return new ObjectResult(problem) { StatusCode = status };
    }
}
