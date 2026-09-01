using Tourism.Domain.Common;

namespace Tourism.Api.Common;

/// <summary>
/// Single place that decides which HTTP status a <see cref="TourismErrorCodes"/> value
/// becomes.
///
/// Kept as one table rather than scattered across controllers so the same failure never
/// answers 404 on one route and 409 on another — inconsistent statuses for the same
/// condition are how clients end up encoding per-endpoint special cases.
/// </summary>
public static class ErrorStatusMap
{
    public static int ToStatusCode(string? errorCode) => errorCode switch
    {
        TourismErrorCodes.InvalidInput => StatusCodes.Status400BadRequest,
        TourismErrorCodes.UnknownCategory => StatusCodes.Status400BadRequest,

        TourismErrorCodes.Unauthorized => StatusCodes.Status401Unauthorized,

        TourismErrorCodes.Forbidden => StatusCodes.Status403Forbidden,

        TourismErrorCodes.NotFound => StatusCodes.Status404NotFound,
        TourismErrorCodes.ProfileNotFound => StatusCodes.Status404NotFound,

        TourismErrorCodes.Conflict => StatusCodes.Status409Conflict,
        TourismErrorCodes.ProfileAlreadyExists => StatusCodes.Status409Conflict,

        // The caller did nothing wrong and retrying later may succeed — that is what 503
        // signals and a generic 500 does not.
        TourismErrorCodes.IdentityServiceUnavailable => StatusCodes.Status503ServiceUnavailable,

        // An unmapped code is a defect in this table, not a client error. 500 makes it
        // visible instead of quietly blaming the caller.
        _ => StatusCodes.Status500InternalServerError
    };
}
