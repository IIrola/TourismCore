namespace Tourism.Domain.Organizations;

/// <summary>
/// The tourism business line, by the code Platform knows it as.
///
/// BIT does not own the list of business lines — Platform declares those — but it does need to
/// recognize its own, because a token scoped to a participation names the vertical it is for.
/// A caller acting in some other vertical's participation must not be able to act here.
/// </summary>
public static class TourismBusinessLine
{
    public const string Code = "tourism";
}

/// <summary>One entry in the tourism catalogue.</summary>
/// <param name="Code">Stable slug stored on a profile.</param>
/// <param name="Name">Human label.</param>
/// <param name="AppliesTo">Which kind of participant may be classified under it.</param>
public sealed record TourismCategory(string Code, string Name, TourismProfileType AppliesTo);

/// <summary>
/// What a tourism participant can be classified as.
///
/// Recovered from the legacy's seeded catalogue, which lived in the identity database and was
/// therefore a tourism taxonomy every other vertical had to carry. Two things it got wrong
/// are fixed here:
///
/// <list type="bullet">
///   <item>The legacy's anonymous sign-up path never validated the classification at all —
///   integer ids arrived straight off an HTTP request and were stored unchecked, so a request
///   omitting them stored category 0, which exists in no catalogue. Only a separate,
///   authenticated endpoint validated. Here there is one rule and every path goes through it.</item>
///   <item>Categories are addressed by a stable code rather than by a seeded integer id. The
///   legacy's ids were assigned by a migration and then embedded into the public identifier
///   itself, which is why that identifier could not be reissued without breaking rows.</item>
/// </list>
/// </summary>
public static class TourismCategories
{
    public static readonly IReadOnlyList<TourismCategory> All =
    [
        new("national-traveller", "National traveller", TourismProfileType.Traveller),
        new("international-traveller", "International traveller", TourismProfileType.Traveller),

        new("lodging", "Lodging and accommodation", TourismProfileType.Operator),
        new("food-and-entertainment", "Food and entertainment", TourismProfileType.Operator),
        new("tourist-transport", "Tourist transport", TourismProfileType.Operator),
        new("agencies-and-tour-operators", "Agencies and tour operators", TourismProfileType.Operator),
        new("guides-and-activities", "Guides and activities", TourismProfileType.Operator),
        new("attractions-and-sites", "Attractions and sites", TourismProfileType.Operator),
        new("health-and-wellness", "Health and wellness", TourismProfileType.Operator),
        new("retail-and-supply", "Retail and supply", TourismProfileType.Operator),
        new("training-and-academia", "Training and academia", TourismProfileType.Operator),
        new("financial-services", "Financial services", TourismProfileType.Operator),
        new("trades-and-support", "Trades and support services", TourismProfileType.Operator),
        new("community-tourism", "Community tourism", TourismProfileType.Operator),
        new("consulting", "Consulting and professional services", TourismProfileType.Operator),
        new("real-estate", "Real estate", TourismProfileType.Operator)
    ];

    /// <summary>The catalogue entry for a code, or null when nothing matches.</summary>
    public static TourismCategory? Find(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var normalized = code.Trim().ToLowerInvariant();
        return All.FirstOrDefault(c => c.Code == normalized);
    }

    /// <summary>
    /// Whether this participant may be classified under this category.
    ///
    /// A traveller cannot be a lodging provider and an operator cannot be an international
    /// traveller — the legacy enforced exactly this pairing, on one of its two paths.
    /// </summary>
    public static bool Permits(TourismProfileType profileType, string? code)
        => Find(code) is { } category && category.AppliesTo == profileType;

    /// <summary>The catalogue as offered to whoever is filling in a classification.</summary>
    public static IReadOnlyList<TourismCategory> For(TourismProfileType profileType)
        => [.. All.Where(c => c.AppliesTo == profileType)];
}
