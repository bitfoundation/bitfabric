namespace Bit.Fabric.Server.Api.Features.Licensing.Models;

public partial class LicensedProjectConfiguration : IEntityTypeConfiguration<LicensedProject>
{
    public void Configure(EntityTypeBuilder<LicensedProject> builder)
    {
        // The id comes from the project's bit.json.
        builder.Property(project => project.Id).ValueGeneratedNever();

        // An account's projects.
        builder.HasIndex(project => project.UserId);

        // An account that is deleted leaves its projects without one, so the next sign-in can claim them again.
        builder.HasOne(project => project.User)
               .WithMany()
               .HasForeignKey(project => project.UserId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(project => project.Licenses)
               .WithOne(license => license.Project)
               .HasForeignKey(license => license.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
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

        builder.HasOne(license => license.User)
               .WithMany()
               .HasForeignKey(license => license.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
