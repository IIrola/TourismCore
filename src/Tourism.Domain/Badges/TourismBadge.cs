namespace Tourism.Domain.Badges;

/// <summary>
/// The badge shown next to a tourism operator in the public directory.
///
/// Mirrors the legacy's shield levels 0-4, with one addition it did not have:
/// <see cref="Undetermined"/>. There the absence of evidence and the worst possible evidence
/// both came out as level 0, so a newly registered operator nobody had checked yet was
/// published looking exactly like one who had been checked and failed. Those are different
/// statements and the directory has to be able to make each of them.
/// </summary>
public enum TourismBadge
{
    /// <summary>Nothing conclusive has been established yet. Legacy had no equivalent.</summary>
    Undetermined = -1,

    /// <summary>Checked, and the evidence does not support a badge. Legacy level 0.</summary>
    None = 0,

    /// <summary>Legacy level 1.</summary>
    Bronze = 1,

    /// <summary>Legacy level 2.</summary>
    Silver = 2,

    /// <summary>Legacy level 3.</summary>
    Gold = 3,

    /// <summary>
    /// Legacy level 4. Not reachable from evidence alone: in the legacy it required a
    /// specific subscription tier. Whether a commercial tier may raise a badge is still an
    /// open business decision, so nothing here awards it.
    /// </summary>
    Platinum = 4
}
