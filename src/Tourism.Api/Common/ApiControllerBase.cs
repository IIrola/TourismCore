using Microsoft.AspNetCore.Mvc;
using Tourism.Domain.Common;

namespace Tourism.Api.Common;

/// <summary>
/// Turns the application layer's <see cref="Result"/> into HTTP responses.
///
/// Failures always come back as <see cref="ProblemDetails"/> carrying the original error
/// code in an <c>errorCode</c> extension, so clients branch on a stable string instead of
/// parsing prose that may be reworded at any time.
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult<T>(Result<T> result)
        => result.IsSuccess
            ? Ok(result.Value)
            : Problem(result.ErrorCode, result.ErrorMessage);

    protected IActionResult FromResult<T>(Result<T> result, Func<T, IActionResult> onSuccess)
        => result.IsSuccess
            ? onSuccess(result.Value!)
            : Problem(result.ErrorCode, result.ErrorMessage);

    protected IActionResult FromResult(Result result)
        => result.IsSuccess
            ? NoContent()
            : Problem(result.ErrorCode, result.ErrorMessage);

    private IActionResult Problem(string? errorCode, string? errorMessage)
    {
        var status = ErrorStatusMap.ToStatusCode(errorCode);
        var problem = new ProblemDetails
        {
            Status = status,
            Title = errorCode ?? "ERROR",
            Detail = errorMessage,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["errorCode"] = errorCode;
        return StatusCode(status, problem);
    }
}
