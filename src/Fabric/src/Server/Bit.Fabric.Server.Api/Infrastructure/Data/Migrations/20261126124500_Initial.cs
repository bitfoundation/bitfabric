using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Bit.Fabric.Server.Api.Infrastructure.Data.Migrations;

/// <inheritdoc />
public partial class _20261126124500_Initial : Migration
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
            name: "ProjectTrials",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                FirstCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IssuedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Document = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Ci = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectTrials", x => x.Id);
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
                HasProfileImage = table.Column<bool>(type: "boolean", nullable: false),
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
            name: "Projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                BitProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                FirstCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Projects", x => x.Id);
                table.UniqueConstraint("AK_Projects_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Projects_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
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
            name: "ProjectScansEmails",
            columns: table => new
            {
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                UserId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                WeekOf = table.Column<DateOnly>(type: "date", nullable: false),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScansEmails", x => new { x.TenantId, x.UserId, x.WeekOf });
                table.ForeignKey(
                    name: "FK_ProjectScansEmails_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectScansEmails_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "TenantMemberships",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                UserId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                AcceptedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_TenantMemberships", x => x.Id);
                table.UniqueConstraint("AK_TenantMemberships_TenantId_UserId", x => new { x.TenantId, x.UserId });
                table.ForeignKey(
                    name: "FK_TenantMemberships_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_TenantMemberships_Users_UserId",
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
            name: "IssuedLicenses",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                IssuedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                Plan = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                FirstCommit = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                IssuedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ExpiresOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Document = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                ClaimCodeHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                ClaimExpiresOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ClaimedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_IssuedLicenses", x => x.Id);
                table.ForeignKey(
                    name: "FK_IssuedLicenses_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_IssuedLicenses_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_IssuedLicenses_Users_IssuedByUserId",
                    column: x => x.IssuedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "Monitorings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                Environment = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                ReportedProjectId = table.Column<Guid>(type: "uuid", nullable: true),
                Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                ServerAddress = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                TemplateVersion = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                IconId = table.Column<Guid>(type: "uuid", nullable: true),
                IconSourceHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                IconReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                HasStaticFrontEnd = table.Column<bool>(type: "boolean", nullable: false),
                Locations = table.Column<string[]>(type: "text[]", nullable: false),
                Health = table.Column<int>(type: "integer", nullable: false),
                ConnectionStatus = table.Column<int>(type: "integer", nullable: false),
                ConnectionProblem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                CreatedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                TimeZoneId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                LastCheckedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Monitorings", x => x.Id);
                table.UniqueConstraint("AK_Monitorings_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_Monitorings_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_Monitorings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
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
            name: "ProjectMembers",
            columns: table => new
            {
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                UserId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                AddedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectMembers", x => new { x.ProjectId, x.UserId });
                table.ForeignKey(
                    name: "FK_ProjectMembers_Projects_TenantId_ProjectId",
                    columns: x => new { x.TenantId, x.ProjectId },
                    principalTable: "Projects",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectMembers_TenantMemberships_TenantId_UserId",
                    columns: x => new { x.TenantId, x.UserId },
                    principalTable: "TenantMemberships",
                    principalColumns: new[] { "TenantId", "UserId" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectMembers_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectMembers_Users_UserId",
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
            name: "ProjectAlertRoutes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Step = table.Column<int>(type: "integer", nullable: false),
                DelayMinutes = table.Column<int>(type: "integer", nullable: false),
                Channel = table.Column<int>(type: "integer", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                WebhookFormat = table.Column<int>(type: "integer", nullable: true),
                Culture = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectAlertRoutes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectAlertRoutes_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectAlertRoutes_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectAlertRoutes_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "ProjectCheckPeriods",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                CheckKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                Title = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                Status = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectCheckPeriods", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectCheckPeriods_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectCheckPeriods_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectConnections",
            columns: table => new
            {
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Issuer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                TokenEndpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Resource = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Scopes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                ClientId = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                ProtectedAccessToken = table.Column<string>(type: "text", nullable: false),
                ProtectedRefreshToken = table.Column<string>(type: "text", nullable: true),
                AccessTokenExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ConnectedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ConnectedOn = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectConnections", x => x.MonitoringId);
                table.ForeignKey(
                    name: "FK_ProjectConnections_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectConnections_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectConnections_Users_ConnectedByUserId",
                    column: x => x.ConnectedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProjectIncidents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Impact = table.Column<int>(type: "integer", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CheckKeys = table.Column<string[]>(type: "text[]", nullable: false),
                ComponentIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                IsAutomatic = table.Column<bool>(type: "boolean", nullable: false),
                AfterDeployAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                DuringMaintenance = table.Column<bool>(type: "boolean", nullable: false),
                AcknowledgedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                AcknowledgedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                AcknowledgedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EscalationStep = table.Column<int>(type: "integer", nullable: false),
                NextEscalationAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                Title = table.Column<string>(type: "jsonb", nullable: true),
                Updates = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectIncidents", x => x.Id);
                table.UniqueConstraint("AK_ProjectIncidents_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ProjectIncidents_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectIncidents_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectMaintenanceWindows",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                StartsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                EndsAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                RepeatsWeekly = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectMaintenanceWindows", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectMaintenanceWindows_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectMaintenanceWindows_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectProbes",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                Colo = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                Outcome = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: true),
                ResponseMs = table.Column<int>(type: "integer", nullable: true),
                VisitorMs = table.Column<int>(type: "integer", nullable: true),
                ReportMs = table.Column<int>(type: "integer", nullable: true),
                HasCacheWarning = table.Column<bool>(type: "boolean", nullable: false),
                ApplicationVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                InstanceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Problem = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                Checks = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectProbes", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectProbes_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectProbes_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectRollups",
            columns: table => new
            {
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                BucketStart = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Region = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                CheckKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Count = table.Column<int>(type: "integer", nullable: false),
                FailedCount = table.Column<int>(type: "integer", nullable: false),
                DegradedCount = table.Column<int>(type: "integer", nullable: false),
                TimedCount = table.Column<int>(type: "integer", nullable: false),
                TotalMs = table.Column<long>(type: "bigint", nullable: false),
                MaxMs = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectRollups", x => new { x.MonitoringId, x.BucketStart, x.Region, x.CheckKey });
                table.ForeignKey(
                    name: "FK_ProjectRollups_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectRollups_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectScanFindingReports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                FindingId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<int>(type: "integer", nullable: false),
                FindingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Where = table.Column<string[]>(type: "text[]", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: true),
                ReportedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScanFindingReports", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectScanFindingReports_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScanFindingReports_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectScanFindingReports_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProjectScans",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Trigger = table.Column<int>(type: "integer", nullable: false),
                RequestedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                ApplicationVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                RequestedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Outcome = table.Column<int>(type: "integer", nullable: true),
                Verdict = table.Column<int>(type: "integer", nullable: true),
                Score = table.Column<int>(type: "integer", nullable: true),
                Grade = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: true),
                ExpiresAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                Pages = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScans", x => x.Id);
                table.UniqueConstraint("AK_ProjectScans_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_ProjectScans_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScans_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
                table.ForeignKey(
                    name: "FK_ProjectScans_Users_RequestedByUserId",
                    column: x => x.RequestedByUserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "ProjectScanSettings",
            columns: table => new
            {
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                ScanAfterDeployments = table.Column<bool>(type: "boolean", nullable: false),
                WeeklyScans = table.Column<bool>(type: "boolean", nullable: false),
                WeeklyEmail = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScanSettings", x => x.MonitoringId);
                table.ForeignKey(
                    name: "FK_ProjectScanSettings_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScanSettings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectSlaSettings",
            columns: table => new
            {
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetPercent = table.Column<double>(type: "double precision", nullable: false),
                PartialWeight = table.Column<double>(type: "double precision", nullable: false),
                DegradedWeight = table.Column<double>(type: "double precision", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                Credits = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectSlaSettings", x => x.MonitoringId);
                table.ForeignKey(
                    name: "FK_ProjectSlaSettings_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectSlaSettings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectStarts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                At = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ApplicationVersion = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                InstanceName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                IsDeploy = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectStarts", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectStarts_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectStarts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "StatusPages",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                Slug = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                ShowsResponseTime = table.Column<bool>(type: "boolean", nullable: false),
                Languages = table.Column<string[]>(type: "text[]", nullable: false),
                SupportAddress = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                CustomHostname = table.Column<string>(type: "character varying(253)", maxLength: 253, nullable: true),
                CustomHostnameId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                CustomHostnameState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusPages", x => x.Id);
                table.UniqueConstraint("AK_StatusPages_TenantId_Id", x => new { x.TenantId, x.Id });
                table.ForeignKey(
                    name: "FK_StatusPages_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StatusPages_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectAlerts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                MonitoringId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                IncidentId = table.Column<Guid>(type: "uuid", nullable: true),
                Kind = table.Column<int>(type: "integer", nullable: false),
                Channel = table.Column<int>(type: "integer", nullable: false),
                Target = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                SentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Succeeded = table.Column<bool>(type: "boolean", nullable: false),
                Problem = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                ProblemStatusCode = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectAlerts", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectAlerts_Monitorings_TenantId_MonitoringId",
                    columns: x => new { x.TenantId, x.MonitoringId },
                    principalTable: "Monitorings",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectAlerts_ProjectIncidents_TenantId_IncidentId",
                    columns: x => new { x.TenantId, x.IncidentId },
                    principalTable: "ProjectIncidents",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectAlerts_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProjectScanFindings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                ScanId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                Severity = table.Column<int>(type: "integer", nullable: false),
                Parameters = table.Column<string[]>(type: "text[]", nullable: false),
                Where = table.Column<string[]>(type: "text[]", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProjectScanFindings", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProjectScanFindings_ProjectScans_TenantId_ScanId",
                    columns: x => new { x.TenantId, x.ScanId },
                    principalTable: "ProjectScans",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProjectScanFindings_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "StatusComponents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                StatusPageId = table.Column<Guid>(type: "uuid", nullable: false),
                TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                Order = table.Column<int>(type: "integer", nullable: false),
                CheckKeys = table.Column<string[]>(type: "text[]", nullable: false),
                IsShown = table.Column<bool>(type: "boolean", nullable: false),
                xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                Description = table.Column<string>(type: "jsonb", nullable: true),
                Name = table.Column<string>(type: "jsonb", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusComponents", x => x.Id);
                table.ForeignKey(
                    name: "FK_StatusComponents_StatusPages_TenantId_StatusPageId",
                    columns: x => new { x.TenantId, x.StatusPageId },
                    principalTable: "StatusPages",
                    principalColumns: new[] { "TenantId", "Id" },
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StatusComponents_Tenants_TenantId",
                    column: x => x.TenantId,
                    principalTable: "Tenants",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "StatusPageFollowers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "uuidv7()"),
                StatusPageId = table.Column<Guid>(type: "uuid", nullable: false),
                PushNotificationSubscriptionId = table.Column<int>(type: "integer", nullable: false),
                Culture = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                ComponentIds = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                FollowedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StatusPageFollowers", x => x.Id);
                table.ForeignKey(
                    name: "FK_StatusPageFollowers_PushNotificationSubscriptions_PushNotif~",
                    column: x => x.PushNotificationSubscriptionId,
                    principalTable: "PushNotificationSubscriptions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_StatusPageFollowers_StatusPages_StatusPageId",
                    column: x => x.StatusPageId,
                    principalTable: "StatusPages",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
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
            columns: new[] { "Id", "AccessFailedCount", "BirthDate", "ConcurrencyStamp", "CreatedOn", "ElevatedAccessTokenRequestedOn", "Email", "EmailConfirmed", "EmailTokenRequestedOn", "FullName", "Gender", "HasProfileImage", "LockoutEnabled", "LockoutEnd", "NormalizedEmail", "NormalizedUserName", "OtpRequestedOn", "PasswordHash", "PhoneNumber", "PhoneNumberConfirmed", "PhoneNumberTokenRequestedOn", "ResetPasswordTokenRequestedOn", "SecurityStamp", "TwoFactorEnabled", "TwoFactorTokenRequestedOn", "UserName" },
            values: new object[,]
            {
                { new Guid("4ff71671-a1d6-4f97-abb9-d87d7b47d6e4"), 0, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "435e1a26-5b3a-4544-8e91-2760cd28e229", new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "store-user@bitfabric.co", true, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Store tenant user", 0, false, true, null, "STORE-USER@BITFABRIC.CO", "STORE-USER", null, "AQAAAAIAAYagAAAAEBK7Er86aEFNjxeC4LeiVtD3p/Qm8ZfvfYN/6jbyermwDMsN+ms0mRyMlFSF7IkZRQ==", null, false, null, null, "469ff4a9-4b07-4cc1-8141-c5fc033daf84", false, null, "store-user" },
                { new Guid("6ff71671-a1d6-4f97-abb9-d87d7b47d6e5"), 0, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "425e1a26-5b3a-4544-8e91-2760cd28e230", new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "store-admin@bitfabric.co", true, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Store tenant admin", 0, false, true, null, "STORE-ADMIN@BITFABRIC.CO", "STORE-ADMIN", null, "AQAAAAIAAYagAAAAEBK7Er86aEFNjxeC4LeiVtD3p/Qm8ZfvfYN/6jbyermwDMsN+ms0mRyMlFSF7IkZRQ==", null, false, null, null, "869ff4a9-4b07-4cc1-8141-c5fc033daf82", false, null, "store-admin" },
                { new Guid("8ff71671-a1d6-4f97-abb9-d87d7b47d6e7"), 0, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "315e1a26-5b3a-4544-8e91-2760cd28e231", new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), null, "test@bitfabric.co", true, new DateTimeOffset(new DateTime(2023, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "Bit.Fabric test account", 0, false, true, null, "TEST@BITFABRIC.CO", "TEST", null, "AQAAAAIAAYagAAAAEBK7Er86aEFNjxeC4LeiVtD3p/Qm8ZfvfYN/6jbyermwDMsN+ms0mRyMlFSF7IkZRQ==", "+31684207362", true, null, null, "959ff4a9-4b07-4cc1-8141-c5fc033daf83", false, null, "test" }
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
            values: new object[] { new Guid("a8c94d94-0004-4dd0-921c-255e0a581424"), "You are Ava, the assistant of the Bit.Fabric app.\n\n## The app\n- It runs on Android, iOS, Windows, macOS and the web as a PWA: [website]({{WebAppUrl}}), [Google Play](https://play.google.com/store/apps/details?id=com.bitplatform.AdminPanel.Template), [App Store](https://apps.apple.com/us/app/bit-adminpanel/id6450611349), [Windows installer](https://windows-adminpanel.bitfabric.co/AdminPanel.Client.Windows-win-Setup.exe).\n- Asked whether it, or an app built with bit Fabric, runs on one or some of these, say yes and name every one of them: Android, iOS, Windows, macOS and the web.\n- Only the `GetAppPages` tool knows the app's pages: call it before you name, link, suggest or open one, and use only what it returns. Link a page by its relative url, like [Sign up](/sign-up), and offer to open it with the `NavigateToPage` tool.\n- A page that requires sign-in needs {{IsAuthenticated}} to be true. If it isn't, the `ShowSignInModal` tool lets the user sign in; greet them once they have.\n\n## What bit Fabric is\n- bit Fabric builds the user's own app with them: they answer a few questions, paste one line, and their project opens on their computer, full featured from day one, so their AI builds their idea rather than the basics. No sign-in is needed to start.\n- Privacy first: bit Fabric never sees their code, data or prompts. Zero lock-in: standard, open code that runs anywhere. They own the code, the server and their users' data.\n\n## What every project comes with\nAsked whether bit Fabric, or an app built with it, has, supports or comes with something listed here, say yes in a sentence or two. Asked about some of the platforms, name the rest too: every project has Android, iOS, Windows and macOS apps and a website. Don't claim anything that isn't listed: say it isn't built in, and that their AI can add it to their project.\n- Platforms: Android, iOS, Windows and macOS apps with unlimited native access (camera, files, notifications...), a fast website ready for Google and Bing, an admin panel, multi-tenant and SaaS ready, GDPR friendly (users choose what they allow, download their data and delete their account), works offline and syncs, force update.\n- Sign-in, with the accounts in their own database: social sign-in (Google, Apple, Microsoft...), two-step sign-in by SMS, email or an authenticator app, magic links (a link by email, with a 6-digit code in it too), quick sign-in in a dialog without leaving the page, back on the same page even from another device, Face ID and fingerprint, elevated sessions, privileged sessions that limit an account's devices, several accounts, features by user group, sign-in from an AI with OAuth 2.1, Keycloak single sign-on, reCAPTCHA, a web application firewall and DDoS protection.\n- An AI assistant like you: stays on topic, knows the user's device, searches their data, safe with images, hands over to a person, asks the user to confirm on screen before anything it can't undo (human in the loop), speech to text, text to speech, live voice and file uploads, a system prompt they change in the app, Microsoft Agent Framework, and MCP, so their users add the app to grok.com or another AI app as a connector, sign in and use it from there.\n- Languages: right to left and left to right, the time zone the user picks and other calendars, an AI translator, every language's pages found by search engines.\n- The Workbench: pages designed with the app's real components on sample data, on every device, theme and state, with any AI; pages side by side on phones, tablets and desktops, store screenshots for the App Store, Google Play and the Microsoft Store, and flows that show how pages lead to one another.\n- Growth: store reviews asked for in the app, email and SMS, free push notifications, Universal Links and App Links, rewarded ads, payments.\n- Admin panel: a live dashboard, no lost changes when two people edit the same thing, PostgreSQL, or SQL Server, MySQL or SQLite.\n- A Windows app: portable, runs on Windows 7 and later, updates itself, kiosk mode.\n- Batteries included: 100+ components (data grid, full calendar scheduler, resource timeline, map, PDF viewer, charts, file upload, rich text editor, date range picker, tree view), ready and tested, which is one reason their AI uses fewer tokens and costs less, and 2,300+ icons, an image editor, a QR code and barcode scanner, and AI skills to rebrand, port an existing app, deploy to their own server, troubleshoot the live app and build with AI-DLC and automated UI tests, and dev-mcp, which lets their own AI, not bit Fabric, read the live app's data and logs, read-only. Their AI brings in the team's 500+ improvements a month and keeps their own changes.\n- Fast and watched over: caching, telemetry with smart sampling, zero downtime deployment (a new version goes live with no downtime), and once live, bit Fabric's health monitoring (of the app, of the other sites its health check lists, such as its landing page or a WordPress blog, and of the SPF and DMARC of the domain its emails are sent from, so sign-in codes and receipts don't land in spam), security, performance, SEO and accessibility scanners (bit Fabric sees every new deployment in the health check it reads all the time, and runs the scanners on each one whenever the owner allows it), a status page on their own domain, and alerts in Slack, Microsoft Teams and Discord as well as by push, email and SMS.\n- They build on macOS, Windows or Linux, with Copilot, Codex, Claude, Gemini, Antigravity, Cursor or a model on their own computer.\n\n## Starting a project\nAnswer questions about starting from these facts only; for anything they don't cover, say the page explains it and offer to open it.\n- It's free and needs no account. The new project page asks a few questions, each with a recommended answer and a \"Help me choose\": the name; a real product or a quick try (SQLite and a sample, no database server); what it starts with (the essentials, an admin panel or a shop); one organisation or several customer companies (SaaS, multi-tenancy); the AI assistant with live updates and push notifications; where errors and logs go (their own server, recommended and free; Sentry, which also catches the phone apps' crashes; or Azure Application Insights, both paid beyond their free tiers); which apps to set up on their own computer. Any answer can change later by asking their AI.\n- Every project has all five apps. Its pipeline builds every one for release, even in a private GitHub repository, on GitHub's free monthly minutes. Setting some up on their own computer as well lets them try them as they build without using those minutes, for a bigger download and an install; the rest can be set up any time later. The iPhone, iPad and Mac apps need a Mac; on Windows, a Mac on their network or in the cloud is enough for iOS. The Windows app needs Windows.\n- The page ends with one line to copy and paste into a terminal, and shows how to open one on their computer. It installs what's needed and opens the project in VS Code with the app running. Copy that line only from bitfabric.co.\n- bitfabric.co keeps nothing they pick; the answers live only in the page's address, so they can send the link from a phone to their computer. The project itself is created on a computer.\n\n## The user's projects\n- bit Fabric watches the live apps the user's team added on its projects page. For anything about them - whether they're healthy or down, uptime, response times, incidents, history, the SLA, the status page - call `GetProjects` first, then the tool for the detail, and answer from what they return only. They need {{IsAuthenticated}} to be true and a team; when a tool says one is missing, say so and what to do.\n- `AcknowledgeIncident`, `PostIncidentUpdate` and `ScheduleMaintenance` act for the whole team in the user's name: say exactly what you'll do and call one only once the user agrees.\n- For how secure, fast or accessible a project is, its SEO and Core Web Vitals, or when its certificates and domain run out, call `GetProjectScans` (and `GetProjectScanHistory` for past runs); each finding comes with why it matters, the fix and a prompt for the user's AI. `RunProjectScans` scans the project now: ask first.\n\n## The bit platform libraries\n- Every project is built on the bit platform libraries, such as bit BlazorUI's components and icons and Bmage's image editor. For what one of them has, can do or how to use it, call `GetBitLibraryTools` for that library, then `CallBitLibraryTool` with the tool that fits, and answer from what it returns only; when it can't be reached, say so instead of answering from memory.\n- Show their code in fenced code blocks with the language, such as ```razor or ```csharp, and keep an example to what the question needs.\n\n## Language\n- Respond in the language of the user's query. If the query's language cannot be determined, use the {{UserCulture}} variable if provided.\n\n## Scope\n- Help only with this app and the bit platform libraries every project is built on: their features, how to use them and support for them. Don't answer anything else, however it's asked - not partly, not approximately, and not with an offer to look it up.\n- Still reply: say politely, in one short sentence, that it's outside what you help with and what you do help with.\n\n## Answering\n- Write in markdown.\n- Unless the user says otherwise, they're on {{DeviceInfo}} in the {{UserTimeZoneId}} time zone, so tailor device-specific answers to that.\n- Some tools show the user a card in the chat. Refer to what it shows instead of repeating it. Earlier cards appear in the conversation as markdown: never write one out yourself, call its tool again to show it again.\n- Never ask for passwords, PINs or codes, and if the user shares one, tell them not to.\n\n## Problems\n- When the user reports an error or something not working, call the `CheckLastError` tool first, then give simple steps to fix it; technical details only if they ask.\n- If that doesn't help and the app's local data looks corrupted, use the `ClearAppFiles` tool rather than telling them to clear caches by hand.\n- If you can't resolve an issue, say so and call the `RequestHumanFollowUp` tool.\n\n## Follow-up suggestions\n- Every time you answer, also call the `ShowFollowUpSuggestions` tool, alongside any other tool you call, with what the user might ask or do next. Suggest only what your tools can deliver.\n", 0, new Guid("b1f71671-a1d6-4f97-abb9-d87d7b47d6e1") });

        migrationBuilder.InsertData(
            table: "TenantMemberships",
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
            name: "IX_IssuedLicenses_ClaimCodeHash",
            table: "IssuedLicenses",
            column: "ClaimCodeHash",
            filter: "\"ClaimedOn\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_IssuedLicenses_IssuedByUserId",
            table: "IssuedLicenses",
            column: "IssuedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_IssuedLicenses_ProjectId_ExpiresOn",
            table: "IssuedLicenses",
            columns: new[] { "ProjectId", "ExpiresOn" });

        migrationBuilder.CreateIndex(
            name: "IX_IssuedLicenses_TenantId_ProjectId",
            table: "IssuedLicenses",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_Monitorings_ProjectId_Environment",
            table: "Monitorings",
            columns: new[] { "ProjectId", "Environment" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Monitorings_TenantId_Address",
            table: "Monitorings",
            columns: new[] { "TenantId", "Address" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Monitorings_TenantId_ProjectId",
            table: "Monitorings",
            columns: new[] { "TenantId", "ProjectId" });

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
            name: "IX_ProjectAlertRoutes_MonitoringId_Step",
            table: "ProjectAlertRoutes",
            columns: new[] { "MonitoringId", "Step" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlertRoutes_TenantId_MonitoringId",
            table: "ProjectAlertRoutes",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlertRoutes_UserId",
            table: "ProjectAlertRoutes",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_IncidentId",
            table: "ProjectAlerts",
            column: "IncidentId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_MonitoringId_SentAt",
            table: "ProjectAlerts",
            columns: new[] { "MonitoringId", "SentAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_TenantId_IncidentId",
            table: "ProjectAlerts",
            columns: new[] { "TenantId", "IncidentId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectAlerts_TenantId_MonitoringId",
            table: "ProjectAlerts",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectCheckPeriods_MonitoringId",
            table: "ProjectCheckPeriods",
            column: "MonitoringId",
            filter: "\"EndedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectCheckPeriods_MonitoringId_CheckKey_StartedAt",
            table: "ProjectCheckPeriods",
            columns: new[] { "MonitoringId", "CheckKey", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectCheckPeriods_TenantId_MonitoringId",
            table: "ProjectCheckPeriods",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectConnections_ConnectedByUserId",
            table: "ProjectConnections",
            column: "ConnectedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectConnections_TenantId_MonitoringId",
            table: "ProjectConnections",
            columns: new[] { "TenantId", "MonitoringId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectIncidents_MonitoringId",
            table: "ProjectIncidents",
            column: "MonitoringId",
            filter: "\"EndedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectIncidents_MonitoringId_StartedAt",
            table: "ProjectIncidents",
            columns: new[] { "MonitoringId", "StartedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectIncidents_TenantId_MonitoringId",
            table: "ProjectIncidents",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMaintenanceWindows_MonitoringId_StartsAt",
            table: "ProjectMaintenanceWindows",
            columns: new[] { "MonitoringId", "StartsAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMaintenanceWindows_TenantId_MonitoringId",
            table: "ProjectMaintenanceWindows",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMembers_TenantId_ProjectId",
            table: "ProjectMembers",
            columns: new[] { "TenantId", "ProjectId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMembers_TenantId_UserId",
            table: "ProjectMembers",
            columns: new[] { "TenantId", "UserId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectMembers_UserId",
            table: "ProjectMembers",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectProbes_At",
            table: "ProjectProbes",
            column: "At");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectProbes_MonitoringId_At",
            table: "ProjectProbes",
            columns: new[] { "MonitoringId", "At" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectProbes_TenantId_MonitoringId",
            table: "ProjectProbes",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectRollups_BucketStart",
            table: "ProjectRollups",
            column: "BucketStart");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectRollups_MonitoringId_CheckKey_BucketStart",
            table: "ProjectRollups",
            columns: new[] { "MonitoringId", "CheckKey", "BucketStart" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectRollups_TenantId_MonitoringId",
            table: "ProjectRollups",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_Projects_BitProjectId",
            table: "Projects",
            column: "BitProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_Projects_TenantId_BitProjectId",
            table: "Projects",
            columns: new[] { "TenantId", "BitProjectId" },
            unique: true,
            filter: "\"BitProjectId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_FindingId_UserId",
            table: "ProjectScanFindingReports",
            columns: new[] { "FindingId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_FindingKey_ReportedAt",
            table: "ProjectScanFindingReports",
            columns: new[] { "FindingKey", "ReportedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_TenantId_MonitoringId",
            table: "ProjectScanFindingReports",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindingReports_UserId",
            table: "ProjectScanFindingReports",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanFindings_TenantId_ScanId",
            table: "ProjectScanFindings",
            columns: new[] { "TenantId", "ScanId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_MonitoringId_Kind_RequestedAt",
            table: "ProjectScans",
            columns: new[] { "MonitoringId", "Kind", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_MonitoringId_RequestedAt",
            table: "ProjectScans",
            columns: new[] { "MonitoringId", "RequestedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_RequestedAt",
            table: "ProjectScans",
            column: "RequestedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_RequestedByUserId",
            table: "ProjectScans",
            column: "RequestedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_TenantId_MonitoringId",
            table: "ProjectScans",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScans_Unfinished",
            table: "ProjectScans",
            column: "MonitoringId",
            filter: "\"FinishedAt\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScansEmails_UserId",
            table: "ProjectScansEmails",
            column: "UserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScansEmails_WeekOf",
            table: "ProjectScansEmails",
            column: "WeekOf");

        migrationBuilder.CreateIndex(
            name: "IX_ProjectScanSettings_TenantId_MonitoringId",
            table: "ProjectScanSettings",
            columns: new[] { "TenantId", "MonitoringId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectSlaSettings_TenantId_MonitoringId",
            table: "ProjectSlaSettings",
            columns: new[] { "TenantId", "MonitoringId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStarts_MonitoringId_At",
            table: "ProjectStarts",
            columns: new[] { "MonitoringId", "At" });

        migrationBuilder.CreateIndex(
            name: "IX_ProjectStarts_TenantId_MonitoringId",
            table: "ProjectStarts",
            columns: new[] { "TenantId", "MonitoringId" });

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
            name: "IX_StatusComponents_TenantId_StatusPageId",
            table: "StatusComponents",
            columns: new[] { "TenantId", "StatusPageId" });

        migrationBuilder.CreateIndex(
            name: "IX_StatusPageFollowers_PushNotificationSubscriptionId",
            table: "StatusPageFollowers",
            column: "PushNotificationSubscriptionId");

        migrationBuilder.CreateIndex(
            name: "IX_StatusPageFollowers_StatusPageId_PushNotificationSubscripti~",
            table: "StatusPageFollowers",
            columns: new[] { "StatusPageId", "PushNotificationSubscriptionId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_CustomHostname",
            table: "StatusPages",
            column: "CustomHostname",
            unique: true,
            filter: "\"CustomHostname\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_MonitoringId",
            table: "StatusPages",
            column: "MonitoringId",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_Slug",
            table: "StatusPages",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_StatusPages_TenantId_MonitoringId",
            table: "StatusPages",
            columns: new[] { "TenantId", "MonitoringId" });

        migrationBuilder.CreateIndex(
            name: "IX_SystemPrompts_TenantId_PromptKind",
            table: "SystemPrompts",
            columns: new[] { "TenantId", "PromptKind" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TenantMemberships_TenantId_UserId",
            table: "TenantMemberships",
            columns: new[] { "TenantId", "UserId" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_TenantMemberships_TenantId_UserId_Accepted",
            table: "TenantMemberships",
            columns: new[] { "TenantId", "UserId" },
            filter: "\"AcceptedOn\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_TenantMemberships_UserId",
            table: "TenantMemberships",
            column: "UserId");

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
            name: "IssuedLicenses");

        migrationBuilder.DropTable(
            name: "OAuthAuthorizationCodes");

        migrationBuilder.DropTable(
            name: "OAuthGrants");

        migrationBuilder.DropTable(
            name: "ProjectAlertRoutes");

        migrationBuilder.DropTable(
            name: "ProjectAlerts");

        migrationBuilder.DropTable(
            name: "ProjectCheckPeriods");

        migrationBuilder.DropTable(
            name: "ProjectConnections");

        migrationBuilder.DropTable(
            name: "ProjectMaintenanceWindows");

        migrationBuilder.DropTable(
            name: "ProjectMembers");

        migrationBuilder.DropTable(
            name: "ProjectProbes");

        migrationBuilder.DropTable(
            name: "ProjectRollups");

        migrationBuilder.DropTable(
            name: "ProjectScanFindingReports");

        migrationBuilder.DropTable(
            name: "ProjectScanFindings");

        migrationBuilder.DropTable(
            name: "ProjectScansEmails");

        migrationBuilder.DropTable(
            name: "ProjectScanSettings");

        migrationBuilder.DropTable(
            name: "ProjectSlaSettings");

        migrationBuilder.DropTable(
            name: "ProjectStarts");

        migrationBuilder.DropTable(
            name: "ProjectTrials");

        migrationBuilder.DropTable(
            name: "RoleClaims");

        migrationBuilder.DropTable(
            name: "StatusComponents");

        migrationBuilder.DropTable(
            name: "StatusPageFollowers");

        migrationBuilder.DropTable(
            name: "SystemPrompts");

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
            name: "ProjectIncidents");

        migrationBuilder.DropTable(
            name: "TenantMemberships");

        migrationBuilder.DropTable(
            name: "ProjectScans");

        migrationBuilder.DropTable(
            name: "PushNotificationSubscriptions");

        migrationBuilder.DropTable(
            name: "StatusPages");

        migrationBuilder.DropTable(
            name: "Roles");

        migrationBuilder.DropTable(
            name: "UserSessions");

        migrationBuilder.DropTable(
            name: "Monitorings");

        migrationBuilder.DropTable(
            name: "Users");

        migrationBuilder.DropTable(
            name: "Projects");

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
