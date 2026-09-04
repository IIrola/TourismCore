using FluentAssertions;
using MediatR;
using NSubstitute;
using Tourism.Application.Organizations.Ports;
using Tourism.Application.PublicDirectory.Ports;
using Tourism.Application.PublicDirectory.Queries;
using Tourism.Domain.Badges;
using Tourism.Domain.Common;
using Tourism.Domain.Organizations;

namespace Tourism.Application.Tests.PublicDirectory;

/// <summary>
/// The public page. Every test here is about the same property: the identity facts are the
/// engine's to release, and a page exists only if the engine released something.
/// </summary>
public class PublicOperatorTests
{
    private static readonly DateTime Now = new(2026, 9, 4, 12, 0, 0, DateTimeKind.Utc);
    private const string DirectoryId = "7K3M9QP2XVZ4";

    private readonly ITourismOrganizationProfileRepository _profiles =
        Substitute.For<ITourismOrganizationProfileRepository>();

    private readonly IPublicDirectoryClient _directory = Substitute.For<IPublicDirectoryClient>();

    public PublicOperatorTests()
    {
        _directory.GetPublishedAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns(Result<PublishedIdentity>.Ok(Identity()));

        _profiles.GetByPublicDirectoryIdAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns(Profile());
    }

    private static PublishedIdentity Identity(int? score = 820) => new(
        DirectoryId, "Ada Lovelace", score, "Level1", 1.0m, "None", "About me",
        [new PublishedContact(DirectoryChannel.Email, "ad***@example.com", IsMasked: true)],
        Now);

    private static TourismOrganizationProfile Profile()
    {
        var profile = TourismOrganizationProfile.Create(
            Guid.NewGuid(), Guid.NewGuid(), TourismProfileType.Operator,
            TourismCategories.All.First(c => c.AppliesTo != TourismProfileType.Traveller).Code, Now);

        profile.RecordProofOfLife(Now);
        profile.RecordPublicDirectoryId(DirectoryId);
        return profile;
    }

    private GetPublicOperatorQueryHandler Handler() => new(_profiles, _directory);

    [Fact]
    public async Task A_page_composes_the_engines_facts_with_tourisms_own()
    {
        var result = await Handler().Handle(new GetPublicOperatorQuery(DirectoryId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Identity.Score.Should().Be(820);

        // The label lives here, where "Guías de Turistas" belongs. In the legacy it was a
        // column on the platform's own user record.
        result.Value.CategoryLabel.Should().NotBeNullOrEmpty();
        result.Value.ProfileType.Should().Be(TourismProfileType.Operator);
    }

    [Fact]
    public async Task Nothing_published_means_no_page_however_much_tourism_still_holds()
    {
        // The whole point of the consent being the engine's to enforce rather than each
        // vertical's to remember.
        _directory.GetPublishedAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns(Result<PublishedIdentity>.Fail(TourismErrorCodes.NotFound));

        var result = await Handler().Handle(new GetPublicOperatorQuery(DirectoryId), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(TourismErrorCodes.NotFound);
    }

    [Fact]
    public async Task The_engine_is_asked_before_this_services_own_records_are_read()
    {
        // Order matters: reading the local profile first and only then checking consent would
        // put a withdrawn operator's data one forgotten branch away from being published.
        _directory.GetPublishedAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns(Result<PublishedIdentity>.Fail(TourismErrorCodes.NotFound));

        await Handler().Handle(new GetPublicOperatorQuery(DirectoryId), CancellationToken.None);

        await _profiles.DidNotReceive().GetByPublicDirectoryIdAsync(
            Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_subject_listed_through_another_vertical_has_no_tourism_page()
    {
        // A person can be in the directory without being a tourism operator.
        _profiles.GetByPublicDirectoryIdAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns((TourismOrganizationProfile?)null);

        var result = await Handler().Handle(new GetPublicOperatorQuery(DirectoryId), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.ProfileNotFound);
    }

    [Fact]
    public async Task The_engine_being_unreachable_is_not_reported_as_a_missing_page()
    {
        // A dependency being down is not a statement about whether somebody is listed.
        _directory.GetPublishedAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns(Result<PublishedIdentity>.Fail(TourismErrorCodes.IdentityServiceUnavailable));

        var result = await Handler().Handle(new GetPublicOperatorQuery(DirectoryId), CancellationToken.None);

        result.ErrorCode.Should().Be(TourismErrorCodes.IdentityServiceUnavailable);
    }

    [Fact]
    public async Task A_withheld_score_leaves_the_page_standing_without_one()
    {
        // The badge still shows: it is tourism's own conclusion, and it was reached from
        // evidence the operator agreed to have evaluated even if not published.
        _directory.GetPublishedAsync(DirectoryId, Arg.Any<CancellationToken>())
            .Returns(Result<PublishedIdentity>.Ok(Identity(score: null)));

        var result = await Handler().Handle(new GetPublicOperatorQuery(DirectoryId), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Identity.Score.Should().BeNull();
    }
}

/// <summary>
/// The lookup. Its predecessor created identity profiles and spent money on a paid provider,
/// from an anonymous GET.
/// </summary>
public class LookupPublicOperatorTests
{
    private const string DirectoryId = "7K3M9QP2XVZ4";

    private readonly IMediator _mediator = Substitute.For<IMediator>();
    private readonly IPublicDirectoryClient _directory = Substitute.For<IPublicDirectoryClient>();

    private LookupPublicOperatorQueryHandler Handler() => new(_mediator, _directory);

    [Fact]
    public async Task A_findable_contact_leads_to_its_owners_page()
    {
        _directory.LookupAsync(DirectoryChannel.Email, "ada@example.com", Arg.Any<CancellationToken>())
            .Returns(Result<string?>.Ok(DirectoryId));

        await Handler().Handle(
            new LookupPublicOperatorQuery(DirectoryChannel.Email, "ada@example.com"), CancellationToken.None);

        await _mediator.Received(1).Send(
            Arg.Is<GetPublicOperatorQuery>(q => q.PublicDirectoryId == DirectoryId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Every_negative_answer_looks_the_same()
    {
        // Not on the platform, not listed, not findable, or that contact withheld — the
        // engine gives one shape of answer for all of them, and any distinction here would
        // be a way of confirming a contact belongs to somebody.
        _directory.LookupAsync(Arg.Any<DirectoryChannel>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result<string?>.Ok(null));

        var result = await Handler().Handle(
            new LookupPublicOperatorQuery(DirectoryChannel.Email, "nobody@example.com"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(TourismErrorCodes.NotFound);
        await _mediator.DidNotReceive().Send(Arg.Any<GetPublicOperatorQuery>(), Arg.Any<CancellationToken>());
    }
}
