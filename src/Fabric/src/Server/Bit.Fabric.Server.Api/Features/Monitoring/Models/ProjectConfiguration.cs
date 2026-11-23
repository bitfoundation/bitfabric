namespace Bit.Fabric.Server.Api.Features.Monitoring.Models;

public partial class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        // One project per address in a team; another team may watch the same app with its own approval.
        builder.HasIndex(project => new { project.TenantId, project.Address }).IsUnique();

        builder.HasOne(project => project.Connection)
               .WithOne(connection => connection.Project)
               .HasForeignKey<ProjectConnection>(connection => connection.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectConnectionConfiguration : IEntityTypeConfiguration<ProjectConnection>
{
    public void Configure(EntityTypeBuilder<ProjectConnection> builder)
    {
        builder.HasKey(connection => connection.ProjectId);
    }
}

public partial class ProjectProbeConfiguration : IEntityTypeConfiguration<ProjectProbe>
{
    public void Configure(EntityTypeBuilder<ProjectProbe> builder)
    {
        // Every page reads a project's rounds newest first, over a span of time.
        builder.HasIndex(probe => new { probe.ProjectId, probe.At });

        // The retention job deletes every project's old rounds by time alone.
        builder.HasIndex(probe => probe.At);

        builder.HasOne(probe => probe.Project)
               .WithMany()
               .HasForeignKey(probe => probe.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(probe => probe.Checks, checks => checks.ToJson());
    }
}

public partial class ProjectCheckPeriodConfiguration : IEntityTypeConfiguration<ProjectCheckPeriod>
{
    public void Configure(EntityTypeBuilder<ProjectCheckPeriod> builder)
    {
        builder.HasIndex(period => new { period.ProjectId, period.CheckKey, period.StartedAt });

        // Every round and every card reads the open ones: a handful among months of closed periods.
        builder.HasIndex(period => period.ProjectId).HasFilter($"\"{nameof(ProjectCheckPeriod.EndedAt)}\" IS NULL");

        builder.HasOne(period => period.Project)
               .WithMany()
               .HasForeignKey(period => period.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectStartConfiguration : IEntityTypeConfiguration<ProjectStart>
{
    public void Configure(EntityTypeBuilder<ProjectStart> builder)
    {
        builder.HasIndex(start => new { start.ProjectId, start.At });

        builder.HasOne(start => start.Project)
               .WithMany()
               .HasForeignKey(start => start.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectRollupConfiguration : IEntityTypeConfiguration<ProjectRollup>
{
    public void Configure(EntityTypeBuilder<ProjectRollup> builder)
    {
        // Performance reads every part of a project over a span of time; a round upserts its rows by this key.
        builder.HasKey(rollup => new { rollup.ProjectId, rollup.BucketStart, rollup.Region, rollup.CheckKey });

        // The cards read one part, the report, of every project over 30 days.
        builder.HasIndex(rollup => new { rollup.ProjectId, rollup.CheckKey, rollup.BucketStart });

        // The retention job deletes every project's old rows by time alone.
        builder.HasIndex(rollup => rollup.BucketStart);

        builder.HasOne(rollup => rollup.Project)
               .WithMany()
               .HasForeignKey(rollup => rollup.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectIncidentConfiguration : IEntityTypeConfiguration<ProjectIncident>
{
    public void Configure(EntityTypeBuilder<ProjectIncident> builder)
    {
        builder.HasIndex(incident => new { incident.ProjectId, incident.StartedAt });

        // Every round looks for its project's open one, and the escalation job for every open one.
        builder.HasIndex(incident => incident.ProjectId).HasFilter($"\"{nameof(ProjectIncident.EndedAt)}\" IS NULL");

        builder.HasOne(incident => incident.Project)
               .WithMany()
               .HasForeignKey(incident => incident.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(incident => incident.Title, title => title.ToJson());

        builder.OwnsMany(incident => incident.Updates, updates =>
        {
            updates.ToJson();
            updates.OwnsMany(update => update.Text);
        });
    }
}

public partial class ProjectMaintenanceWindowConfiguration : IEntityTypeConfiguration<ProjectMaintenanceWindow>
{
    public void Configure(EntityTypeBuilder<ProjectMaintenanceWindow> builder)
    {
        builder.HasIndex(window => new { window.ProjectId, window.StartsAt });

        builder.HasOne(window => window.Project)
               .WithMany()
               .HasForeignKey(window => window.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectSlaSettingsConfiguration : IEntityTypeConfiguration<ProjectSlaSettings>
{
    public void Configure(EntityTypeBuilder<ProjectSlaSettings> builder)
    {
        builder.HasKey(settings => settings.ProjectId);

        builder.HasOne(settings => settings.Project)
               .WithOne()
               .HasForeignKey<ProjectSlaSettings>(settings => settings.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.OwnsMany(settings => settings.Credits, credits => credits.ToJson());
    }
}

public partial class ProjectAlertRouteConfiguration : IEntityTypeConfiguration<ProjectAlertRoute>
{
    public void Configure(EntityTypeBuilder<ProjectAlertRoute> builder)
    {
        builder.HasIndex(route => new { route.ProjectId, route.Step });

        builder.HasOne(route => route.Project)
               .WithMany()
               .HasForeignKey(route => route.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        // A member who leaves takes their routes with them.
        builder.HasOne(route => route.User)
               .WithMany()
               .HasForeignKey(route => route.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectAlertConfiguration : IEntityTypeConfiguration<ProjectAlert>
{
    public void Configure(EntityTypeBuilder<ProjectAlert> builder)
    {
        builder.HasIndex(alert => new { alert.ProjectId, alert.SentAt });

        builder.HasIndex(alert => alert.IncidentId);

        // By name, so a reason added later never reads as another.
        builder.Property(alert => alert.Problem).HasConversion<string>().HasMaxLength(30);

        builder.HasOne(alert => alert.Project)
               .WithMany()
               .HasForeignKey(alert => alert.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(alert => alert.Incident)
               .WithMany()
               .HasForeignKey(alert => alert.IncidentId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class StatusPageConfiguration : IEntityTypeConfiguration<StatusPage>
{
    public void Configure(EntityTypeBuilder<StatusPage> builder)
    {
        builder.HasIndex(page => page.ProjectId).IsUnique();

        // Across every tenant: a slug and a host name lead to one page.
        builder.HasIndex(page => page.Slug).IsUnique();
        builder.HasIndex(page => page.CustomHostname).IsUnique().HasFilter($"\"{nameof(StatusPage.CustomHostname)}\" IS NOT NULL");

        builder.HasOne(page => page.Project)
               .WithMany()
               .HasForeignKey(page => page.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(page => page.Components)
               .WithOne(component => component.StatusPage)
               .HasForeignKey(component => component.StatusPageId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class StatusComponentConfiguration : IEntityTypeConfiguration<StatusComponent>
{
    public void Configure(EntityTypeBuilder<StatusComponent> builder)
    {
        builder.OwnsMany(component => component.Name, name => name.ToJson());
        builder.OwnsMany(component => component.Description, description => description.ToJson());
    }
}

public partial class StatusPageFollowerConfiguration : IEntityTypeConfiguration<StatusPageFollower>
{
    public void Configure(EntityTypeBuilder<StatusPageFollower> builder)
    {
        builder.HasIndex(follower => new { follower.StatusPageId, follower.PushNotificationSubscriptionId }).IsUnique();

        builder.HasOne(follower => follower.StatusPage)
               .WithMany()
               .HasForeignKey(follower => follower.StatusPageId)
               .OnDelete(DeleteBehavior.Cascade);

        // A browser that stops accepting pushes stops following.
        builder.HasOne(follower => follower.PushNotificationSubscription)
               .WithMany()
               .HasForeignKey(follower => follower.PushNotificationSubscriptionId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectScanConfiguration : IEntityTypeConfiguration<ProjectScan>
{
    public void Configure(EntityTypeBuilder<ProjectScan> builder)
    {
        // The tab's latest run of each kind and its history of one kind, newest first.
        builder.HasIndex(scan => new { scan.ProjectId, scan.Kind, scan.RequestedAt });

        // Every kind's history, and "Run scans now"'s 10 minutes.
        builder.HasIndex(scan => new { scan.ProjectId, scan.RequestedAt });

        // The retention job deletes every project's old runs by time alone.
        builder.HasIndex(scan => scan.RequestedAt);

        // A request joins the run of its kind still waiting, and the weekly job tidies the ones that never ran: a handful.
        builder.HasIndex(scan => scan.ProjectId, "IX_ProjectScans_Unfinished").HasFilter($"\"{nameof(ProjectScan.FinishedAt)}\" IS NULL");

        builder.HasOne(scan => scan.Project)
               .WithMany()
               .HasForeignKey(scan => scan.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(scan => scan.RequestedBy)
               .WithMany()
               .HasForeignKey(scan => scan.RequestedByUserId)
               .OnDelete(DeleteBehavior.SetNull);

        builder.HasMany(scan => scan.Findings)
               .WithOne(finding => finding.Scan)
               .HasForeignKey(finding => finding.ScanId)
               .OnDelete(DeleteBehavior.Cascade);

        // A handful a run, always read with it.
        builder.OwnsMany(scan => scan.Pages, pages => pages.ToJson());
    }
}

public partial class ProjectScanSettingsConfiguration : IEntityTypeConfiguration<ProjectScanSettings>
{
    public void Configure(EntityTypeBuilder<ProjectScanSettings> builder)
    {
        builder.HasKey(settings => settings.ProjectId);

        builder.HasOne(settings => settings.Project)
               .WithOne()
               .HasForeignKey<ProjectScanSettings>(settings => settings.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}

public partial class ProjectScanFindingReportConfiguration : IEntityTypeConfiguration<ProjectScanFindingReport>
{
    public void Configure(EntityTypeBuilder<ProjectScanFindingReport> builder)
    {
        // A member reports a finding once; the tab marks the findings someone reported.
        builder.HasIndex(report => new { report.FindingId, report.UserId }).IsUnique();

        // The guard metric: how often each finding is said to be wrong.
        builder.HasIndex(report => new { report.FindingKey, report.ReportedAt });

        builder.HasOne(report => report.Project)
               .WithMany()
               .HasForeignKey(report => report.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(report => report.User)
               .WithMany()
               .HasForeignKey(report => report.UserId)
               .OnDelete(DeleteBehavior.SetNull);
    }
}

public partial class ProjectScansEmailConfiguration : IEntityTypeConfiguration<ProjectScansEmail>
{
    public void Configure(EntityTypeBuilder<ProjectScansEmail> builder)
    {
        // One a week a member of a team: the job claims it by inserting this key before it sends.
        builder.HasKey(email => new { email.TenantId, email.UserId, email.WeekOf });

        // The job forgets the weeks gone by.
        builder.HasIndex(email => email.WeekOf);

        builder.HasOne(email => email.User)
               .WithMany()
               .HasForeignKey(email => email.UserId)
               .OnDelete(DeleteBehavior.Cascade);
    }
}
