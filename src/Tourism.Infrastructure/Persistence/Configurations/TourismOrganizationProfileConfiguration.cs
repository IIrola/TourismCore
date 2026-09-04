using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tourism.Domain.Organizations;

namespace Tourism.Infrastructure.Persistence.Configurations;

public sealed class TourismOrganizationProfileConfiguration : IEntityTypeConfiguration<TourismOrganizationProfile>
{
    public void Configure(EntityTypeBuilder<TourismOrganizationProfile> builder)
    {
        builder.ToTable("TourismOrganizationProfiles");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.OrganizationId).IsRequired();

        // One organization takes part in this vertical at most once — enforced here, not
        // merely assumed by the repository's lookup-then-create flow, so a race between two
        // concurrent registrations cannot create two profiles for the same organization.
        builder.HasIndex(p => p.OrganizationId)
            .IsUnique()
            .HasDatabaseName("IX_TourismOrganizationProfiles_OrganizationId");

        builder.Property(p => p.ProfileType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(p => p.CategoryCode)
            .IsRequired()
            .HasMaxLength(ColumnLengths.CategoryCode);

        builder.Property(p => p.RegisteredAtUtc).IsRequired();

        builder.Property(p => p.LastProofOfLifeAtUtc);

        // Nullable enum: no badge has been decided yet for a freshly registered operator.
        builder.Property(p => p.CurrentBadge)
            .HasConversion<int>();

        builder.Property(p => p.BadgeAssessedAtUtc);

        builder.Property(p => p.LastEvaluationId);

        builder.Property(p => p.PublicDirectoryId)
            .HasMaxLength(ColumnLengths.PublicDirectoryId);

        // The lookup that serves the public page. Unique because it is what an external link
        // points at: two operators sharing one would make a link ambiguous.
        builder.HasIndex(p => p.PublicDirectoryId)
            .IsUnique()
            .HasDatabaseName("IX_TourismOrganizationProfiles_PublicDirectoryId");

        builder.Property(p => p.BadgeReasons)
            .HasMaxLength(ColumnLengths.BadgeReasons);
    }
}
