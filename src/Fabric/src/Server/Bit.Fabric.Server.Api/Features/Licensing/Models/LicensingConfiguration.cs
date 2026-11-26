namespace Bit.Fabric.Server.Api.Features.Licensing.Models;

public partial class ProjectTrialConfiguration : IEntityTypeConfiguration<ProjectTrial>
{
    public void Configure(EntityTypeBuilder<ProjectTrial> builder)
    {
        // The id comes from the project's bit.json.
        builder.Property(trial => trial.Id).ValueGeneratedNever();
    }
}

public partial class IssuedLicenseConfiguration : IEntityTypeConfiguration<IssuedLicense>
{
    public void Configure(EntityTypeBuilder<IssuedLicense> builder)
    {
        // A project's licenses, newest first, for whether it has one in force.
        builder.HasIndex(license => new { license.ProjectId, license.ExpiresOn });

        // bit CLI asks by its code every few seconds while it waits; only codes not yet spent are looked up.
        builder.HasIndex(license => license.ClaimCodeHash).HasFilter($"\"{nameof(IssuedLicense.ClaimedOn)}\" IS NULL");

        builder.HasOne(license => license.Project)
               .WithMany(project => project.Licenses)
               .HasForeignKey(license => license.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        // The license stays the team's when the account that got it is deleted.
        builder.HasOne(license => license.IssuedBy)
               .WithMany()
               .HasForeignKey(license => license.IssuedByUserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}
