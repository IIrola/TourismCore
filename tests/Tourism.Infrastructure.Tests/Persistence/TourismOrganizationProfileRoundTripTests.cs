using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Tourism.Domain.Badges;
using Tourism.Domain.Organizations;
using Tourism.Infrastructure.Persistence;
using Tourism.Infrastructure.Persistence.Repositories;

namespace Tourism.Infrastructure.Tests.Persistence;

/// <summary>
/// <see cref="TourismOrganizationProfile"/> exposes only a private constructor and
/// setter-less properties; everything on it is populated through <c>Create</c>,
/// <c>RecordProofOfLife</c> and <c>RecordBadge</c> alone. These tests prove the mapping — not
/// the domain — can round-trip that shape through a second, freshly-created
/// <see cref="TourismDbContext"/>, and that the one-profile-per-organization invariant is
/// actually enforced by the database, not just assumed by the repository's lookup-then-create
/// flow.
/// </summary>
public sealed class TourismOrganizationProfileRoundTripTests : IDisposable
{
    private static readonly DateTime NowUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly SqliteContextFixture _fixture = new();
    private TourismDbContext Context => _fixture.Context;

    [Fact]
    public async Task A_freshly_registered_profile_RoundTrips_WithNoBadgeYet()
    {
        var organizationId = Guid.NewGuid();
        var profile = TourismOrganizationProfile.Create(organizationId, Guid.NewGuid(), TourismProfileType.Operator, "Tour-Guide", NowUtc);

        var repository = new TourismOrganizationProfileRepository(Context);
        await repository.AddAsync(profile);
        await new UnitOfWork(Context).SaveChangesAsync();

        await using var freshContext = OpenFreshContext();
        var reloaded = await new TourismOrganizationProfileRepository(freshContext).GetByOrganizationAsync(organizationId);

        reloaded.Should().NotBeNull();
        reloaded!.OrganizationId.Should().Be(organizationId);
        reloaded.ProfileType.Should().Be(TourismProfileType.Operator);
        // Proves normalization happened before storage, not just on the in-memory instance.
        reloaded.CategoryCode.Should().Be("tour-guide");
        reloaded.CurrentBadge.Should().BeNull("a nullable enum with no value yet must round-trip as null, not as a default");
        reloaded.LastEvaluationId.Should().BeNull();
        reloaded.BadgeReasons.Should().BeNull();
    }

    [Fact]
    public async Task ProofOfLife_and_a_recorded_badge_RoundTrip_together()
    {
        var organizationId = Guid.NewGuid();
        var profile = TourismOrganizationProfile.Create(organizationId, Guid.NewGuid(), TourismProfileType.Operator, "tour-guide", NowUtc);
        profile.RecordProofOfLife(NowUtc.AddDays(1));
        var evaluationId = Guid.NewGuid();
        profile.RecordBadge(new BadgeDecision(TourismBadge.Gold, ["Strong evidence.", "Recently active."]), evaluationId, NowUtc.AddDays(2));

        await new TourismOrganizationProfileRepository(Context).AddAsync(profile);
        await new UnitOfWork(Context).SaveChangesAsync();

        await using var freshContext = OpenFreshContext();
        var reloaded = await new TourismOrganizationProfileRepository(freshContext).GetByOrganizationAsync(organizationId);

        reloaded.Should().NotBeNull();
        reloaded!.LastProofOfLifeAtUtc.Should().Be(NowUtc.AddDays(1));
        reloaded.CurrentBadge.Should().Be(TourismBadge.Gold);
        reloaded.BadgeAssessedAtUtc.Should().Be(NowUtc.AddDays(2));
        reloaded.LastEvaluationId.Should().Be(evaluationId);
        reloaded.BadgeReasons.Should().Contain("Strong evidence.").And.Contain("Recently active.");
    }

    [Fact]
    public async Task GetByOrganizationAsync_ReturnsNull_WhenNoProfileExists()
    {
        var found = await new TourismOrganizationProfileRepository(Context).GetByOrganizationAsync(Guid.NewGuid());

        found.Should().BeNull();
    }

    [Fact]
    public async Task OrganizationId_UniqueIndex_Rejects_ASecondProfileForTheSameOrganization()
    {
        var organizationId = Guid.NewGuid();
        var first = TourismOrganizationProfile.Create(organizationId, Guid.NewGuid(), TourismProfileType.Operator, "tour-guide", NowUtc);
        await new TourismOrganizationProfileRepository(Context).AddAsync(first);
        await new UnitOfWork(Context).SaveChangesAsync();

        // A second, distinct profile claiming the same organization is exactly the scenario
        // the unique index exists to prevent — an organization takes part in this vertical
        // at most once.
        var second = TourismOrganizationProfile.Create(organizationId, Guid.NewGuid(), TourismProfileType.Traveller, "traveller", NowUtc);
        await new TourismOrganizationProfileRepository(Context).AddAsync(second);

        var act = () => new UnitOfWork(Context).SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    private TourismDbContext OpenFreshContext() => _fixture.CreateAdditionalContext();

    public void Dispose() => _fixture.Dispose();
}
