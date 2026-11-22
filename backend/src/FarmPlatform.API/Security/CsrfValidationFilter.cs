using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FarmPlatform.Api.Security;

/// <summary>
/// Validates the antiforgery token on every state-changing request (POST/PUT/PATCH/DELETE) to a controller.
/// Registered globally, so new endpoints are protected by default.
/// </summary>
public sealed partial class CsrfValidationFilter(IAntiforgery antiforgery, ILogger<CsrfValidationFilter> logger) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
        {
            return;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException ex)
        {
            LogCsrfFailed(logger, method, context.HttpContext.Request.Path, ex.Message);
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Invalid or missing CSRF token.",
                Type = "https://farmplatform/errors/csrf",
                Extensions = { ["code"] = "security.csrf_invalid" },
            })
            { StatusCode = StatusCodes.Status400BadRequest };
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "CSRF validation failed for {Method} {Path}: {Reason}")]
    private static partial void LogCsrfFailed(ILogger logger, string method, PathString path, string reason);
}
