namespace Tourism.Infrastructure.Persistence;

/// <summary>
/// Maximum lengths for the string columns of the Tourism schema.
///
/// Every string column declares one explicitly, the same rule Platform and PIMA follow:
/// MariaDB maps an unbounded string to <c>longtext</c>, which cannot be indexed without a
/// prefix — irrelevant here since none of these columns are indexed, but leaving one
/// unbounded is still how a runaway value (a badge explanation nobody capped) ends up
/// silently accepted where a mistake should have been caught instead.
/// </summary>
internal static class ColumnLengths
{
    /// <summary>Tourism catalogue category codes, e.g. "tour-guide", "travel-insurer".</summary>
    public const int CategoryCode = 128;

    /// <summary>
    /// The joined explanation behind a badge decision (see <c>BadgeAssessment</c>). Up to a
    /// handful of sentences are joined with spaces — generous headroom over what the current
    /// rules ever produce, so a slightly longer future explanation does not need a migration.
    /// </summary>
    public const int BadgeReasons = 2000;
}
