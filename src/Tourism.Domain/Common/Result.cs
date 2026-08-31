namespace Tourism.Domain.Common;

/// <summary>
/// Represents the outcome of an operation that can either succeed with a value or fail with a
/// typed error code. Use for expected business failures; reserve exceptions for unexpected
/// technical faults and broken invariants.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
    }

    private Result(string errorCode, string? errorMessage)
    {
        IsSuccess = false;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static Result<T> Ok(T value) => new(value);

    public static Result<T> Fail(string errorCode, string? errorMessage = null) => new(errorCode, errorMessage);
}

public sealed class Result
{
    public bool IsSuccess { get; }
    public string? ErrorCode { get; }
    public string? ErrorMessage { get; }

    private Result(bool ok, string? errorCode, string? errorMessage)
    {
        IsSuccess = ok;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    public static readonly Result Ok = new(true, null, null);

    public static Result Fail(string errorCode, string? errorMessage = null) => new(false, errorCode, errorMessage);
}
