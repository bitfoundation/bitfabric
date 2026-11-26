using Bit.Fabric.Server.Api.Features.Tenants;

namespace Bit.Fabric.Server.Api.Features.Projects.Models;

public partial class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        // One project per bit.json id in a team: what the linking by the app's report and the license page find it by.
        builder.HasIndex(project => new { project.TenantId, project.BitProjectId })
               .IsUnique()
               .HasFilter($"\"{nameof(Project.BitProjectId)}\" IS NOT NULL");

        // What the docs MCP server's check and the trial and conversion figures look projects up by, across teams.
        builder.HasIndex(project => project.BitProjectId);
    }
}

public partial class ProjectMemberConfiguration : IEntityTypeConfiguration<ProjectMember>
{
    public void Configure(EntityTypeBuilder<ProjectMember> builder)
    {
        builder.HasKey(member => new { member.ProjectId, member.UserId });

        builder.HasOne(member => member.Project)
               .WithMany(project => project.Members)
               .HasForeignKey(member => member.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(member => member.User)
               .WithMany()
               .HasForeignKey(member => member.UserId)
               .OnDelete(DeleteBehavior.Cascade);

        // A member of the project is a member of its team: removing someone from the team removes them from its projects.
        builder.HasOne(member => member.Membership)
               .WithMany()
               .HasForeignKey(member => new { member.TenantId, member.UserId })
               .HasPrincipalKey(membership => new { membership.TenantId, membership.UserId })
               .OnDelete(DeleteBehavior.Cascade);

        // A person's projects in a team, which leaving it removes (See TeamDeparture).
        builder.HasIndex(member => new { member.TenantId, member.UserId });
    }
}
