namespace Tourism.Domain.Organizations;

/// <summary>
/// What kind of participant this is in the tourism business.
///
/// Recovered from the legacy's <c>TenantProfileType</c>, which lived on the identity user
/// record and therefore forced every other vertical to carry a tourism concept. It belongs
/// here.
/// </summary>
public enum TourismProfileType
{
    /// <summary>An individual traveller.</summary>
    Traveller = 0,

    /// <summary>A company offering tourism services.</summary>
    Operator = 1
}
