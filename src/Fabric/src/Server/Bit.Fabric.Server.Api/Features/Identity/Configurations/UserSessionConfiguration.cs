namespace Bit.Fabric.Server.Api.Features.Identity.Configurations;

public partial class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.HasIndex(userSession => userSession.SignalRConnectionId)
            .HasFilter($"\"{nameof(UserSession.SignalRConnectionId)}\" IS NOT NULL")
            ;
    }
}
