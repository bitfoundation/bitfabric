using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class Initial : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(
            name: "jobs");

        migrationBuilder.CreateSequence<int>(
            name: "ProductShortId",
            startValue: 10051L);

        migrationBuilder.CreateTable(
            name: "Attachments",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Path = table.Column<string>(type: "text", nullable: true),
                CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Attachments", x => new { x.Id, x.Kind });
            });

        migrationBuilder.CreateTable(
            name: "DataProtectionKeys",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                FriendlyName = table.Column<string>(type: "text", nullable: true),
                Xml = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_DataProtectionKeys", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "HangfireCounter",
            schema: "jobs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Value = table.Column<long>(type: "bigint", nullable: false),
                ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireCounter", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "HangfireHash",
            schema: "jobs",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Field = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Value = table.Column<string>(type: "text", nullable: true),
                ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireHash", x => new { x.Key, x.Field });
            });

        migrationBuilder.CreateTable(
            name: "HangfireList",
            schema: "jobs",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Value = table.Column<string>(type: "text", nullable: true),
                ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireList", x => new { x.Key, x.Position });
            });

        migrationBuilder.CreateTable(
            name: "HangfireLock",
            schema: "jobs",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                AcquiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireLock", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "HangfireServer",
            schema: "jobs",
            columns: table => new
            {
                Id = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Heartbeat = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                WorkerCount = table.Column<int>(type: "integer", nullable: false),
                Queues = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireServer", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "HangfireSet",
            schema: "jobs",
            columns: table => new
            {
                Key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Value = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Score = table.Column<double>(type: "double precision", nullable: false),
                ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireSet", x => new { x.Key, x.Value });
            });

        migrationBuilder.CreateTable(
            name: "Tenants",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Name = table.Column<string>(type: "character varying(63)", maxLength: 63, nullable: false),
                Title = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                Domain = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tenants", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Users",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                FullName = table.Column<string>(type: "text", nullable: true),
                Gender = table.Column<int>(type: "integer", nullable: true),
                BirthDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EmailTokenRequestedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                PhoneNumberTokenRequestedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ResetPasswordTokenRequestedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                TwoFactorTokenRequestedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                OtpRequestedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ElevatedAccessTokenRequestedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                HasProfilePicture = table.Column<bool>(type: "boolean", nullable: false),
                UserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedUserName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                EmailConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                PasswordHash = table.Column<string>(type: "text", nullable: true),
                SecurityStamp = table.Column<string>(type: "text", nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true),
                PhoneNumber = table.Column<string>(type: "text", nullable: true),
                PhoneNumberConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                TwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false),
                LockoutEnd = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LockoutEnabled = table.Column<bool>(type: "boolean", nullable: false),
                AccessFailedCount = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Users", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Roles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                NormalizedName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ConcurrencyStamp = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Roles", x => x.Id);
                table.ForeignKey(
                    name: "FK_Roles_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "SystemPrompts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                PromptKind = table.Column<int>(type: "integer", nullable: false),
                Markdown = table.Column<string>(type: "text", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_SystemPrompts", x => x.Id);
                table.ForeignKey(
                    name: "FK_SystemPrompts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "OAuthAuthorizationCodes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                CodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                ClientId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                RedirectUri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                Resource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                Scope = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                CodeChallenge = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Amr = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                ExpiresOn = table.Column<long>(type: "bigint", nullable: false),
                ConsumedOn = table.Column<long>(type: "bigint", nullable: true),
                UserSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OAuthAuthorizationCodes", x => x.Id);
                table.ForeignKey(
                    name: "FK_OAuthAuthorizationCodes_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey(
                    name: "FK_OAuthAuthorizationCodes_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TenantUsers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                AcceptedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantUsers", x => x.Id);
                table.ForeignKey(
                    name: "FK_TenantUsers_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TenantUsers_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserClaims_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserClaims_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserLogins",
            columns: table => new
            {
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                ProviderKey = table.Column<string>(type: "text", nullable: false),
                ProviderDisplayName = table.Column<string>(type: "text", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserLogins", x => new { x.LoginProvider, x.ProviderKey });
                table.ForeignKey(
                    name: "FK_UserLogins_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserSessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                IP = table.Column<string>(type: "text", nullable: true),
                Address = table.Column<string>(type: "text", nullable: true),
                Privileged = table.Column<bool>(type: "boolean", nullable: false),
                AuthenticationMethod = table.Column<string>(type: "text", nullable: true),
                Trusted = table.Column<bool>(type: "boolean", nullable: false),
                StartedOn = table.Column<long>(type: "bigint", nullable: false),
                RenewedOn = table.Column<long>(type: "bigint", nullable: true),
                PreviousRenewedOn = table.Column<long>(type: "bigint", nullable: true),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true),
                SignalRConnectionId = table.Column<string>(type: "text", nullable: true),
                NotificationStatus = table.Column<int>(type: "integer", nullable: false),
                DeviceInfo = table.Column<string>(type: "text", nullable: true),
                PlatformType = table.Column<int>(type: "integer", nullable: true),
                CultureName = table.Column<string>(type: "text", nullable: true),
                AppVersionCode = table.Column<long>(type: "bigint", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserSessions", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserSessions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserSessions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserTokens",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                LoginProvider = table.Column<string>(type: "text", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                Value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                table.ForeignKey(
                    name: "FK_UserTokens_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "WebAuthnCredential",
            columns: table => new
            {
                Id = table.Column<byte[]>(type: "bytea", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                PublicKey = table.Column<byte[]>(type: "bytea", nullable: true),
                SignCount = table.Column<long>(type: "bigint", nullable: false),
                Transports = table.Column<int[]>(type: "integer[]", nullable: true),
                IsBackupEligible = table.Column<bool>(type: "boolean", nullable: false),
                IsBackedUp = table.Column<bool>(type: "boolean", nullable: false),
                AttestationObject = table.Column<byte[]>(type: "bytea", nullable: true),
                AttestationClientDataJson = table.Column<byte[]>(type: "bytea", nullable: true),
                UserHandle = table.Column<byte[]>(type: "bytea", nullable: true),
                AttestationFormat = table.Column<string>(type: "text", nullable: true),
                RegDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                AaGuid = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_WebAuthnCredential", x => x.Id);
                table.ForeignKey(
                    name: "FK_WebAuthnCredential_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "RoleClaims",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false),
                ClaimType = table.Column<string>(type: "text", nullable: true),
                ClaimValue = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RoleClaims", x => x.Id);
                table.ForeignKey(
                    name: "FK_RoleClaims_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "UserRoles",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                RoleId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserRoles", x => new { x.UserId, x.RoleId });
                table.ForeignKey(
                    name: "FK_UserRoles_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserRoles_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_UserRoles_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "OAuthGrants",
            columns: table => new
            {
                UserSessionId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ClientId = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                ClientName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                Scope = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                Resource = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                RefreshTokenId = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_OAuthGrants", x => x.UserSessionId);
                table.ForeignKey(
                    name: "FK_OAuthGrants_UserSessions_UserSessionId",
                    column: x => x.UserSessionId,
                    principalTable: "UserSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "PushNotificationSubscriptions",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                DeviceId = table.Column<string>(type: "text", nullable: false),
                Platform = table.Column<string>(type: "text", nullable: false),
                PushChannel = table.Column<string>(type: "text", nullable: false),
                P256dh = table.Column<string>(type: "text", nullable: true),
                Auth = table.Column<string>(type: "text", nullable: true),
                Endpoint = table.Column<string>(type: "text", nullable: true),
                UserSessionId = table.Column<Guid>(type: "uuid", nullable: true),
                IP = table.Column<string>(type: "text", nullable: true),
                Address = table.Column<string>(type: "text", nullable: true),
                AppVersionCode = table.Column<long>(type: "bigint", nullable: true),
                Tags = table.Column<string[]>(type: "text[]", nullable: false),
                ExpirationTime = table.Column<long>(type: "bigint", nullable: false),
                RenewedOn = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_PushNotificationSubscriptions", x => x.Id);
                table.ForeignKey(
                    name: "FK_PushNotificationSubscriptions_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_PushNotificationSubscriptions_UserSessions_UserSessionId",
                    column: x => x.UserSessionId,
                    principalTable: "UserSessions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "HangfireJob",
            schema: "jobs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                StateId = table.Column<long>(type: "bigint", nullable: true),
                StateName = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                ExpireAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                InvocationData = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireJob", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "HangfireJobParameter",
            schema: "jobs",
            columns: table => new
            {
                JobId = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Value = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireJobParameter", x => new { x.JobId, x.Name });
                table.ForeignKey(
                    name: "FK_HangfireJobParameter_HangfireJob_JobId",
                    column: x => x.JobId,
                    principalSchema: "jobs",
                    principalTable: "HangfireJob",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "HangfireQueuedJob",
            schema: "jobs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobId = table.Column<long>(type: "bigint", nullable: false),
                Queue = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                FetchedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireQueuedJob", x => x.Id);
                table.ForeignKey(
                    name: "FK_HangfireQueuedJob_HangfireJob_JobId",
                    column: x => x.JobId,
                    principalSchema: "jobs",
                    principalTable: "HangfireJob",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "HangfireState",
            schema: "jobs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                JobId = table.Column<long>(type: "bigint", nullable: false),
                Name = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                Reason = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Data = table.Column<string>(type: "text", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_HangfireState", x => x.Id);
                table.ForeignKey(
                    name: "FK_HangfireState_HangfireJob_JobId",
                    column: x => x.JobId,
                    principalSchema: "jobs",
                    principalTable: "HangfireJob",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.InsertData(
            table: "Roles",
            columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName", "TenantId" },
            values: new object[] { new Guid("8ff71671-a1d6-5f97-abb9-d87d7b47d6e7"), "8ff71671-a1d6-5f97-abb9-d87d7b47d6e7", "g-admin", "G-ADMIN", null });

        migrationBuilder.InsertData(
            table: "Tenants",
            columns: new[] { "Id", "Domain", "IsActive", "Name", "Title" },
            values: new object[] { new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1"), null, true, "store", "Store" });

        migrationBuilder.InsertData(
            table: "Users",
            columns: new[] { "Id", "AccessFailedCount", "BirthDate", "ConcurrencyStamp", "CreatedOn", "ElevatedAccessTokenRequestedOn", "Email", "EmailConfirmed", "EmailTokenRequestedOn", "FullName", "Gender", "HasProfilePicture", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "OtpRequestedOn", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "PhoneNumberTokenRequestedOn", "ResetPasswordTokenRequestedOn", "SecurityStamp", "TwoFactorEnabled", "TwoFactorTokenRequestedOn", "UserName" },
            values: new object[,]
            {
                { new Guid("4ff71671-a1d6-4f97-abb9-d87d7b47d6e4"), 0, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "435e1a26-5b3a-4544-8e91-2760cd28e229", new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "store-user@bitplatform.dev", true, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Store tenant user", 0, false, true, null, "STORE-USER@BITPLATFORM.DEV", "STORE-USER", null, "AQAAAAIAAYagAAAAEBK7Er86aEFNjxeC4LeiVtD3p/Qm8ZfvfYN/6jbyermwDMsN+ms0mRyMlFSF7IkZRQ==", null, false, null, null, "469ff4a9-4b07-4cc1-8141-c5fc033daf84", false, null, "store-user" },
                { new Guid("6ff71671-a1d6-4f97-abb9-d87d7b47d6e5"), 0, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "425e1a26-5b3a-4544-8e91-2760cd28e230", new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "store-admin@bitplatform.dev", true, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Store tenant admin", 0, false, true, null, "STORE-ADMIN@BITPLATFORM.DEV", "STORE-ADMIN", null, "AQAAAAIAAYagAAAAEBK7Er86aEFNjxeC4LeiVtD3p/Qm8ZfvfYN/6jbyermwDMsN+ms0mRyMlFSF7IkZRQ==", null, false, null, null, "869ff4a9-4b07-4cc1-8141-c5fc033daf82", false, null, "store-admin" },
                { new Guid("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7"), 0, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "315e1a26-5b3a-4544-8e91-2760cd28e231", new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "test@bitplatform.dev", true, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bit.Fabric test account", 0, false, true, null, "TEST@BITPLATFORM.DEV", "TEST", null, "AQAAAAIAAYagAAAAEBK7Er86aEFNjxeC4LeiVtD3p/Qm8ZfvfYN/6jbyermwDMsN+ms0mRyMlFSF7IkZRQ==", "+31684207362", true, null, null, "959ff4a9-4b07-4cc1-8141-c5fc033daf83", false, null, "test" }
            });

        migrationBuilder.InsertData(
            table: "RoleClaims",
            columns: new[] { "Id", "ClaimType", "ClaimValue", "RoleId" },
            values: new object[] { -1, "mx-p-s", "-1", new Guid("8ff71671-a1d6-5f97-abb9-d87d7b47d6e7") });

        migrationBuilder.InsertData(
            table: "Roles",
            columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName", "TenantId" },
            values: new object[,]
            {
                { new Guid("7ff71671-a1d6-5f97-abb9-d87d7b47d6e9"), "7ff71671-a1d6-5f97-abb9-d87d7b47d6e9", "t-admin", "T-ADMIN", new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1") },
                { new Guid("9ff71672-a1d5-4f97-abb7-d87d6b47d5e8"), "9ff71672-a1d5-4f97-abb7-d87d6b47d5e8", "demo", "DEMO", new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1") }
            });

        migrationBuilder.InsertData(
            table: "SystemPrompts",
            columns: new[] { "Id", "Markdown", "PromptKind", "TenantId" },
            values: new object[] { new Guid("a8c94d94-0004-4dd0-921c-255e0a581424"), "You are Ava, the assistant of the Bit.Fabric app.\n\n## The app\n- It runs on Android, iOS, Windows, macOS and the web as a PWA: [website]({{WebAppUrl}}), [Google Play](https://play.google.com/store/apps/details?id=com.bitplatform.AdminPanel.Template), [App Store](https://apps.apple.com/us/app/bit-adminpanel/id6450611349), [Windows installer](https://windows-admin.bitplatform.dev/AdminPanel.Client.Windows-win-Setup.exe).\n- Asked whether it, or an app built with bit Fabric, runs on one or some of these, say yes and name every one of them: Android, iOS, Windows, macOS and the web.\n- Only the `GetAppPages` tool knows the app's pages: call it before you name, link, suggest or open one, and use only what it returns. Link a page by its relative url, like [Sign up](/sign-up), and offer to open it with the `NavigateToPage` tool.\n- A page that requires sign-in needs {{IsAuthenticated}} to be true. If it isn't, the `ShowSignInModal` tool lets the user sign in; greet them once they have.\n\n## What bit Fabric is\n- bit Fabric builds the user's own app with them: they answer a few questions, paste one line, and their project opens on their computer, full featured from day one, so their AI builds their idea rather than the basics. No sign-in is needed to start.\n- Privacy first: bit Fabric never sees their code, data or prompts. Zero lock-in: standard, open code that runs anywhere. They own the code, the server and their users' data.\n\n## What every project comes with\nAsked whether bit Fabric, or an app built with it, has, supports or comes with something listed here, say yes in a sentence or two. Asked about some of the platforms, name the rest too: every project has Android, iOS, Windows and macOS apps and a website. Don't claim anything that isn't listed: say it isn't built in, and that their AI can add it to their project.\n- Platforms: Android, iOS, Windows and macOS apps with unlimited native access (camera, files, notifications...), a fast website ready for Google and Bing, an admin panel, multi-tenant and SaaS ready, GDPR friendly (users choose what they allow, download their data and delete their account), works offline and syncs, force update.\n- Sign-in, with the accounts in their own database: social sign-in (Google, Apple, Microsoft...), two-step sign-in by SMS, email or an authenticator app, magic links, back on the same page even from another device, Face ID and fingerprint, elevated sessions, privileged sessions that limit an account's devices, several accounts, features by user group, sign-in from an AI with OAuth 2.1, Keycloak single sign-on, reCAPTCHA, a web application firewall and DDoS protection.\n- An AI assistant like you: stays on topic, knows the user's device, searches their data, safe with images, hands over to a person, speech to text, text to speech, live voice and file uploads, a system prompt they change in the app, Microsoft Agent Framework.\n- Languages: right to left and left to right, the time zone the user picks and other calendars, an AI translator, every language's pages found by search engines.\n- The Workbench: pages designed with the app's real components on sample data, on every device, theme and state, with any AI.\n- Growth: store reviews asked for in the app, email and SMS, free push notifications, Universal Links and App Links, rewarded ads, payments.\n- Admin panel: a live dashboard, no lost changes when two people edit the same thing, PostgreSQL, or SQL Server, MySQL or SQLite.\n- A Windows app: portable, runs on Windows 7 and later, updates itself, kiosk mode.\n- Batteries included: 100+ components (data grid, scheduler, charts, rich text editor, date range picker, tree view) and 2,300+ icons, an image editor, a QR code and barcode scanner, and AI skills to rebrand, port an existing app, deploy to their own server, troubleshoot the live app and build with AI-DLC and automated UI tests. Their AI brings in the team's 500+ improvements a month and keeps their own changes.\n- Fast and watched over: caching, telemetry with smart sampling, and once live, bit Fabric's health monitoring, performance, SEO and accessibility scanners, and a status page on their own domain.\n- They build on macOS, Windows or Linux, with Copilot, Codex, Claude, Gemini, Cursor or a model on their own computer.\n\n## Starting a project\nAnswer questions about starting from these facts only; for anything they don't cover, say the page explains it and offer to open it.\n- It's free and needs no account. The new project page asks a few questions, each with a recommended answer and a \"Help me choose\": the name; a real product or a quick try (SQLite and a sample, no database server); what it starts with (the essentials, an admin panel or a shop); one organisation or several customer companies (SaaS, multi-tenancy); the AI assistant with live updates and push notifications; where errors and logs go (their own server, recommended and free; Sentry, which also catches the phone apps' crashes; or Azure Application Insights, both paid beyond their free tiers); which apps to set up on their own computer. Any answer can change later by asking their AI.\n- Every project has all five apps. Its pipeline builds every one for release, even in a private GitHub repository, on GitHub's free monthly minutes. Setting some up on their own computer as well lets them try them as they build without using those minutes, for a bigger download and an install; the rest can be set up any time later. The iPhone, iPad and Mac apps need a Mac; on Windows, a Mac on their network or in the cloud is enough for iOS. The Windows app needs Windows.\n- The page ends with one line to copy and paste into a terminal, and shows how to open one on their computer. It installs what's needed and opens the project in VS Code with the app running. Copy that line only from bitfabric.co.\n- bitfabric.co keeps nothing they pick; the answers live only in the page's address, so they can send the link from a phone to their computer. The project itself is created on a computer.\n\n## Language\n- Respond in the language of the user's query. If the query's language cannot be determined, use the {{UserCulture}} variable if provided.\n\n## Scope\n- Help only with this app: its features, how to use it and support for it. Don't answer anything else, however it's asked - not partly, not approximately, and not with an offer to look it up.\n- Still reply: say politely, in one short sentence, that it's outside what you help with and what you do help with.\n\n## Answering\n- Write in markdown.\n- Unless the user says otherwise, they're on {{DeviceInfo}} in the {{UserTimeZoneId}} time zone, so tailor device-specific answers to that.\n- Some tools show the user a card in the chat. Refer to what it shows instead of repeating it. Earlier cards appear in the conversation as markdown: never write one out yourself, call its tool again to show it again.\n- Never ask for passwords, PINs or codes, and if the user shares one, tell them not to.\n\n## Problems\n- When the user reports an error or something not working, call the `CheckLastError` tool first, then give simple steps to fix it; technical details only if they ask.\n- If that doesn't help and the app's local data looks corrupted, use the `ClearAppFiles` tool rather than telling them to clear caches by hand.\n- If you can't resolve an issue, say so and call the `RequestHumanFollowUp` tool.\n\n## Follow-up suggestions\n- Every time you answer, also call the `ShowFollowUpSuggestions` tool, alongside any other tool you call, with what the user might ask or do next. Suggest only what your tools can deliver.\n", 0, new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1") });

        migrationBuilder.InsertData(
            table: "TenantUsers",
            columns: new[] { "Id", "AcceptedOn", "TenantId", "UserId" },
            values: new object[,]
            {
                { new Guid("5ff71671-a1d6-4f97-abb9-d87d7b47d6e1"), new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1"), new Guid("4ff71671-a1d6-4f97-abb9-d87d7b47d6e4") },
                { new Guid("5ff71671-a1d6-4f97-abb9-d87d7b47d6e2"), new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1"), new Guid("6ff71671-a1d6-4f97-abb9-d87d7b47d6e5") },
                { new Guid("5ff71671-a1d6-4f97-abb9-d87d7b47d6e3"), new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1"), new Guid("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7") }
            });

        migrationBuilder.InsertData(
            table: "UserRoles",
            columns: new[] { "RoleId", "UserId", "TenantId" },
            values: new object[] { new Guid("8ff71671-a1d6-5f97-abb9-d87d7b47d6e7"), new Guid("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7"), null });

        migrationBuilder.InsertData(
            table: "RoleClaims",
            columns: new[] { "Id", "ClaimType", "ClaimValue", "RoleId" },
            values: new object[,]
            {
                { -5, "features", "4.0", new Guid("9ff71672-a1d5-4f97-abb7-d87d6b47d5e8") },
                { -4, "features", "3.1", new Guid("9ff71672-a1d5-4f97-abb7-d87d6b47d5e8") },
                { -3, "features", "3.0", new Guid("9ff71672-a1d5-4f97-abb7-d87d6b47d5e8") },
                { -2, "mx-p-s", "-1", new Guid("7ff71671-a1d6-5f97-abb9-d87d7b47d6e9") }
            });

        migrationBuilder.InsertData(
            table: "UserRoles",
            columns: new[] { "RoleId", "UserId", "TenantId" },
            values: new object[,]
            {
                { new Guid("9ff71672-a1d5-4f97-abb7-d87d6b47d5e8"), new Guid("4ff71671-a1d6-4f97-abb9-d87d7b47d6e4"), new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1") },
                { new Guid("7ff71671-a1d6-5f97-abb9-d87d7b47d6e9"), new Guid("6ff71671-a1d6-4f97-abb9-d87d7b47d6e5"), new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1") }
            });

        migrationBuilder.CreateIndex(
            name: "IX_HangfireCounter_ExpireAt",
            schema: "jobs",
            table: "HangfireCounter",
            column: "ExpireAt");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireCounter_Key_Value",
            schema: "jobs",
            table: "HangfireCounter",
            columns: new[] { "Key", "Value" });

        migrationBuilder.CreateIndex(
            name: "IX_HangfireHash_ExpireAt",
            schema: "jobs",
            table: "HangfireHash",
            column: "ExpireAt");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireJob_ExpireAt",
            schema: "jobs",
            table: "HangfireJob",
            column: "ExpireAt");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireJob_StateId",
            schema: "jobs",
            table: "HangfireJob",
            column: "StateId");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireJob_StateName",
            schema: "jobs",
            table: "HangfireJob",
            column: "StateName");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireList_ExpireAt",
            schema: "jobs",
            table: "HangfireList",
            column: "ExpireAt");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireQueuedJob_JobId",
            schema: "jobs",
            table: "HangfireQueuedJob",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireQueuedJob_Queue_FetchedAt",
            schema: "jobs",
            table: "HangfireQueuedJob",
            columns: new[] { "Queue", "FetchedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_HangfireServer_Heartbeat",
            schema: "jobs",
            table: "HangfireServer",
            column: "Heartbeat");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireSet_ExpireAt",
            schema: "jobs",
            table: "HangfireSet",
            column: "ExpireAt");

        migrationBuilder.CreateIndex(
            name: "IX_HangfireSet_Key_Score",
            schema: "jobs",
            table: "HangfireSet",
            columns: new[] { "Key", "Score" });

        migrationBuilder.CreateIndex(
            name: "IX_HangfireState_JobId",
            schema: "jobs",
            table: "HangfireState",
            column: "JobId");

        migrationBuilder.CreateIndex(
            name: "IX_OAuthAuthorizationCodes_CodeHash",
            table: "OAuthAuthorizationCodes",
            column: "CodeHash",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_OAuthAuthorizationCodes_ExpiresOn",
            table: "OAuthAuthorizationCodes",
            column: "ExpiresOn");

        migrationBuilder.CreateIndex(
            name: "IX_OAuthAuthorizationCodes_TenantId",
            table: "OAuthAuthorizationCodes",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_OAuthAuthorizationCodes_UserId",
            table: "OAuthAuthorizationCodes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_OAuthGrants_ClientId",
            table: "OAuthGrants",
            column: "ClientId");

        migrationBuilder.CreateIndex(
            name: "IX_PushNotificationSubscriptions_DeviceId",
            table: "PushNotificationSubscriptions",
            column: "DeviceId");

        migrationBuilder.CreateIndex(
            name: "IX_PushNotificationSubscriptions_ExpirationTime",
            table: "PushNotificationSubscriptions",
            column: "ExpirationTime");

        migrationBuilder.CreateIndex(
            name: "IX_PushNotificationSubscriptions_TenantId",
            table: "PushNotificationSubscriptions",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_PushNotificationSubscriptions_UserSessionId",
            table: "PushNotificationSubscriptions",
            column: "UserSessionId",
            unique: true,
            filter: "\"UserSessionId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
            table: "RoleClaims",
            columns: new[] { "RoleId", "ClaimType", "ClaimValue" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Name",
            table: "Roles",
            column: "Name",
            unique: true,
            filter: "\"TenantId\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_Name_TenantId",
            table: "Roles",
            columns: new[] { "Name", "TenantId" },
            unique: true,
            filter: "\"TenantId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Roles_TenantId",
            table: "Roles",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "RoleNameIndex",
            table: "Roles",
            column: "NormalizedName");

        migrationBuilder.CreateIndex(
            name: "IX_SystemPrompts_TenantId_PromptKind",
            table: "SystemPrompts",
            columns: new[] { "TenantId", "PromptKind" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Tenants_Domain",
            table: "Tenants",
            column: "Domain",
            unique: true,
            filter: "\"Domain\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Tenants_Name",
            table: "Tenants",
            column: "Name",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TenantUsers_TenantId_UserId",
            table: "TenantUsers",
            columns: new[] { "TenantId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TenantUsers_TenantId_UserId_Accepted",
            table: "TenantUsers",
            columns: new[] { "TenantId", "UserId" },
            filter: "\"AcceptedOn\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_TenantUsers_UserId",
            table: "TenantUsers",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserClaims_TenantId",
            table: "UserClaims",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_UserClaims_UserId_ClaimType_ClaimValue",
            table: "UserClaims",
            columns: new[] { "UserId", "ClaimType", "ClaimValue" },
            unique: true,
            filter: "\"TenantId\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserClaims_UserId_ClaimType_ClaimValue_TenantId",
            table: "UserClaims",
            columns: new[] { "UserId", "ClaimType", "ClaimValue", "TenantId" },
            unique: true,
            filter: "\"TenantId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserClaims_UserId_TenantId",
            table: "UserClaims",
            columns: new[] { "UserId", "TenantId" });

        migrationBuilder.CreateIndex(
            name: "IX_UserLogins_UserId",
            table: "UserLogins",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_RoleId_UserId",
            table: "UserRoles",
            columns: new[] { "RoleId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserRoles_TenantId",
            table: "UserRoles",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "EmailIndex",
            table: "Users",
            column: "NormalizedEmail",
            unique: true,
            filter: "\"NormalizedEmail\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_Users_CreatedOn",
            table: "Users",
            column: "CreatedOn",
            filter: "NOT \"EmailConfirmed\" AND NOT \"PhoneNumberConfirmed\"");

        migrationBuilder.CreateIndex(
            name: "IX_Users_PhoneNumber",
            table: "Users",
            column: "PhoneNumber",
            unique: true,
            filter: "\"PhoneNumber\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "UserNameIndex",
            table: "Users",
            column: "NormalizedUserName",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_SignalRConnectionId",
            table: "UserSessions",
            column: "SignalRConnectionId",
            filter: "\"SignalRConnectionId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_TenantId",
            table: "UserSessions",
            column: "TenantId");

        migrationBuilder.CreateIndex(
            name: "IX_UserSessions_UserId",
            table: "UserSessions",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_WebAuthnCredential_UserId",
            table: "WebAuthnCredential",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_HangfireJob_HangfireState_StateId",
            schema: "jobs",
            table: "HangfireJob",
            column: "StateId",
            principalSchema: "jobs",
            principalTable: "HangfireState",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_HangfireJob_HangfireState_StateId",
            schema: "jobs",
            table: "HangfireJob");

        migrationBuilder.DropTable(
            name: "Attachments");

        migrationBuilder.DropTable(
            name: "DataProtectionKeys");

        migrationBuilder.DropTable(
            name: "HangfireCounter",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireHash",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireJobParameter",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireList",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireLock",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireQueuedJob",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireServer",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireSet",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "OAuthAuthorizationCodes");

        migrationBuilder.DropTable(
            name: "OAuthGrants");

        migrationBuilder.DropTable(
            name: "PushNotificationSubscriptions");

        migrationBuilder.DropTable(
            name: "RoleClaims");

        migrationBuilder.DropTable(
            name: "SystemPrompts");

        migrationBuilder.DropTable(
            name: "TenantUsers");

        migrationBuilder.DropTable(
            name: "UserClaims");

        migrationBuilder.DropTable(
            name: "UserLogins");

        migrationBuilder.DropTable(
            name: "UserRoles");

        migrationBuilder.DropTable(
            name: "UserTokens");

        migrationBuilder.DropTable(
            name: "WebAuthnCredential");

        migrationBuilder.DropTable(
            name: "UserSessions");

        migrationBuilder.DropTable(
            name: "Roles");

        migrationBuilder.DropTable(
            name: "Users");

        migrationBuilder.DropTable(
            name: "Tenants");

        migrationBuilder.DropTable(
            name: "HangfireState",
            schema: "jobs");

        migrationBuilder.DropTable(
            name: "HangfireJob",
            schema: "jobs");

        migrationBuilder.DropSequence(
            name: "ProductShortId");
    }
}
