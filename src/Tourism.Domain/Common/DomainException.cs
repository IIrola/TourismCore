namespace Tourism.Domain.Common;

/// <summary>
/// Thrown when an aggregate would be left in an invalid state. Reaching this is a defect, not
/// an expected outcome — user-facing validation belongs in the application layer and expected
/// failures are modelled with <see cref="Result"/>.
/// </summary>
public sealed class DomainException(string message) : Exception(message)
{
    public static void ThrowIfNullOrWhiteSpace(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException($"{field} is required.");
    }

    public static void ThrowIfEmpty(Guid value, string field)
    {
        if (value == Guid.Empty)
            throw new DomainException($"{field} is required.");
    }
}
