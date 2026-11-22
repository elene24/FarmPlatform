using Microsoft.AspNetCore.Mvc;

namespace FarmPlatform.Api.Errors;

/// <summary>
/// Response for requests the model binder can't read at all (malformed JSON, wrong types). Uses the same
/// shape as use-case errors: field -> [code], plus a top-level "code".
/// </summary>
public static class InvalidRequestResponse
{
    private static readonly string[] InvalidCode = ["validation.invalid"];

    public static IActionResult Create(ActionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var errors = context.ModelState
            .Where(e => e.Value?.Errors.Count > 0)
            .ToDictionary(
                e => string.IsNullOrEmpty(e.Key) ? "body" : char.ToLowerInvariant(e.Key.TrimStart('$', '.')[0]) + e.Key.TrimStart('$', '.')[1..],
                _ => InvalidCode);

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "validation.invalid_request",
        };
        problem.Extensions["code"] = "validation.invalid_request";
        return new BadRequestObjectResult(problem);
    }
}
