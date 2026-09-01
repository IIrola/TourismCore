using Microsoft.AspNetCore.Mvc;
using Tourism.Domain.Common;

namespace Tourism.Api.Common;

/// <summary>
/// Last line of the pipeline. Turns anything that escaped a handler into a
/// <see cref="ProblemDetails"/> response and keeps internal detail out of it.
///
/// <see cref="DomainException"/> is treated as a 400: reaching it means input got past
/// validation, which is a defect worth surfacing to the caller as a bad request rather than
/// a 500 — but its message is safe to return because the domain only ever states which
/// invariant was broken.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException ex)
        {
            logger.LogWarning(ex, "Domain invariant broken while handling {Path}", context.Request.Path);
            await WriteProblem(context, StatusCodes.Status400BadRequest, TourismErrorCodes.InvalidInput, ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while handling {Path}", context.Request.Path);
            // No exception detail on the wire: it can leak schema, paths and library versions.
            await WriteProblem(context, StatusCodes.Status500InternalServerError, "INTERNAL_ERROR",
                "An unexpected error occurred.");
        }
    }

    private static async Task WriteProblem(HttpContext context, int status, string errorCode, string detail)
    {
        if (context.Response.HasStarted)
            return;

        context.Response.Clear();
        context.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = errorCode,
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["errorCode"] = errorCode;

        await context.Response.WriteAsJsonAsync(problem);
    }
}
