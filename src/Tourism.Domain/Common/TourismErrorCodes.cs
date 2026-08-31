namespace Tourism.Domain.Common;

public static class TourismErrorCodes
{
    public const string InvalidInput = "INVALID_INPUT";
    public const string NotFound = "NOT_FOUND";
    public const string Conflict = "CONFLICT";
    public const string Forbidden = "FORBIDDEN";
    public const string Unauthorized = "UNAUTHORIZED";

    public const string ProfileNotFound = "TOURISM_PROFILE_NOT_FOUND";
    public const string ProfileAlreadyExists = "TOURISM_PROFILE_ALREADY_EXISTS";
    public const string UnknownCategory = "UNKNOWN_TOURISM_CATEGORY";

    /// <summary>The identity engine could not be reached or answered with an error.</summary>
    public const string IdentityServiceUnavailable = "IDENTITY_SERVICE_UNAVAILABLE";
}
