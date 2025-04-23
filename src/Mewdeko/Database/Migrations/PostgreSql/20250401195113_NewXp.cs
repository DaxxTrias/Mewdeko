using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class NewXp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ExcludedItem");

            migrationBuilder.DropTable(
                name: "XpCurrencyReward");

            migrationBuilder.DropTable(
                name: "XpRoleReward");

            migrationBuilder.DropTable(
                name: "XpSettings");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_AwardedXp",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_GuildId",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_UserId",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_UserId_GuildId",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_UserXpStats_Xp",
                table: "UserXpStats");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateBarId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateClubId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateGuildId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateUserId",
                table: "Template");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLevelUp",
                table: "UserXpStats",
                type: "timestamp without time zone",
                nullable: false,
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone",
                oldDefaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 307, DateTimeKind.Local));

            migrationBuilder.CreateTable(
                name: "GuildUserXp",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    TotalXp = table.Column<long>(type: "bigint", nullable: false),
                    BonusXp = table.Column<long>(type: "bigint", nullable: false),
                    LastActivity = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    NotifyType = table.Column<int>(type: "integer", nullable: false),
                    LastLevelUp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildUserXp", x => new { x.GuildId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "GuildXpSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    XpMultiplier = table.Column<double>(type: "double precision", nullable: false),
                    XpPerMessage = table.Column<int>(type: "integer", nullable: false),
                    MessageXpCooldown = table.Column<int>(type: "integer", nullable: false),
                    VoiceXpPerMinute = table.Column<int>(type: "integer", nullable: false),
                    VoiceXpTimeout = table.Column<int>(type: "integer", nullable: false),
                    FirstMessageBonus = table.Column<int>(type: "integer", nullable: false),
                    XpCurveType = table.Column<int>(type: "integer", nullable: false),
                    XpGainDisabled = table.Column<bool>(type: "boolean", nullable: false),
                    CustomXpImageUrl = table.Column<string>(type: "text", nullable: false),
                    LevelUpMessage = table.Column<string>(type: "text", nullable: false),
                    ExclusiveRoleRewards = table.Column<bool>(type: "boolean", nullable: false),
                    EnableXpDecay = table.Column<bool>(type: "boolean", nullable: false),
                    InactivityDaysBeforeDecay = table.Column<int>(type: "integer", nullable: false),
                    DailyDecayPercentage = table.Column<double>(type: "double precision", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuildXpSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XpBoostEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Multiplier = table.Column<double>(type: "double precision", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ApplicableChannels = table.Column<string>(type: "text", nullable: false),
                    ApplicableRoles = table.Column<string>(type: "text", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpBoostEvents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XpChannelMultipliers",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Multiplier = table.Column<double>(type: "double precision", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpChannelMultipliers", x => new { x.GuildId, x.ChannelId });
                });

            migrationBuilder.CreateTable(
                name: "XpCompetitionEntries",
                columns: table => new
                {
                    CompetitionId = table.Column<int>(type: "integer", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StartingXp = table.Column<long>(type: "bigint", nullable: false),
                    CurrentXp = table.Column<long>(type: "bigint", nullable: false),
                    AchievedTargetAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    FinalPlacement = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpCompetitionEntries", x => new { x.CompetitionId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "XpCompetitionRewards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetitionId = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    XpAmount = table.Column<int>(type: "integer", nullable: false),
                    CurrencyAmount = table.Column<long>(type: "bigint", nullable: false),
                    CustomReward = table.Column<string>(type: "text", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpCompetitionRewards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XpCompetitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    StartTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    EndTime = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TargetLevel = table.Column<int>(type: "integer", nullable: false),
                    AnnouncementChannelId = table.Column<decimal>(type: "numeric(20,0)", nullable: true),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpCompetitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "XpCurrencyRewards",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpCurrencyRewards", x => new { x.GuildId, x.Level });
                });

            migrationBuilder.CreateTable(
                name: "XpExcludedItems",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ItemId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpExcludedItems", x => new { x.GuildId, x.ItemId, x.ItemType });
                });

            migrationBuilder.CreateTable(
                name: "XpRoleMultipliers",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Multiplier = table.Column<double>(type: "double precision", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpRoleMultipliers", x => new { x.GuildId, x.RoleId });
                });

            migrationBuilder.CreateTable(
                name: "XpRoleRewards",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpRoleRewards", x => new { x.GuildId, x.Level });
                });

            migrationBuilder.CreateTable(
                name: "XpRoleTracking",
                columns: table => new
                {
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    StartTracking = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalXpGained = table.Column<long>(type: "bigint", nullable: false),
                    EndTracking = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    TrackingTitle = table.Column<string>(type: "text", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpRoleTracking", x => new { x.GuildId, x.RoleId, x.StartTracking });
                });

            migrationBuilder.CreateTable(
                name: "XpUserSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GuildId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    UserId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    TotalXp = table.Column<long>(type: "bigint", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpUserSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Template_GuildId",
                table: "Template",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateBarId",
                table: "Template",
                column: "TemplateBarId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateClubId",
                table: "Template",
                column: "TemplateClubId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateGuildId",
                table: "Template",
                column: "TemplateGuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateUserId",
                table: "Template",
                column: "TemplateUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserXp_GuildId",
                table: "GuildUserXp",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserXp_LastActivity",
                table: "GuildUserXp",
                column: "LastActivity");

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserXp_TotalXp",
                table: "GuildUserXp",
                column: "TotalXp");

            migrationBuilder.CreateIndex(
                name: "IX_GuildUserXp_UserId",
                table: "GuildUserXp",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_XpChannelMultipliers_GuildId",
                table: "XpChannelMultipliers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_XpCompetitionEntries_AchievedTargetAt",
                table: "XpCompetitionEntries",
                column: "AchievedTargetAt");

            migrationBuilder.CreateIndex(
                name: "IX_XpCompetitionEntries_CompetitionId",
                table: "XpCompetitionEntries",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_XpCompetitionEntries_CurrentXp",
                table: "XpCompetitionEntries",
                column: "CurrentXp");

            migrationBuilder.CreateIndex(
                name: "IX_XpCompetitionEntries_UserId",
                table: "XpCompetitionEntries",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_XpCurrencyRewards_GuildId",
                table: "XpCurrencyRewards",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_XpExcludedItems_GuildId",
                table: "XpExcludedItems",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_XpExcludedItems_ItemId_ItemType",
                table: "XpExcludedItems",
                columns: new[] { "ItemId", "ItemType" });

            migrationBuilder.CreateIndex(
                name: "IX_XpRoleMultipliers_GuildId",
                table: "XpRoleMultipliers",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_XpRoleRewards_GuildId",
                table: "XpRoleRewards",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_XpRoleTracking_EndTracking",
                table: "XpRoleTracking",
                column: "EndTracking");

            migrationBuilder.CreateIndex(
                name: "IX_XpRoleTracking_GuildId",
                table: "XpRoleTracking",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_XpRoleTracking_RoleId",
                table: "XpRoleTracking",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_XpUserSnapshots_GuildId_UserId",
                table: "XpUserSnapshots",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_XpUserSnapshots_Timestamp",
                table: "XpUserSnapshots",
                column: "Timestamp");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuildUserXp");

            migrationBuilder.DropTable(
                name: "GuildXpSettings");

            migrationBuilder.DropTable(
                name: "XpBoostEvents");

            migrationBuilder.DropTable(
                name: "XpChannelMultipliers");

            migrationBuilder.DropTable(
                name: "XpCompetitionEntries");

            migrationBuilder.DropTable(
                name: "XpCompetitionRewards");

            migrationBuilder.DropTable(
                name: "XpCompetitions");

            migrationBuilder.DropTable(
                name: "XpCurrencyRewards");

            migrationBuilder.DropTable(
                name: "XpExcludedItems");

            migrationBuilder.DropTable(
                name: "XpRoleMultipliers");

            migrationBuilder.DropTable(
                name: "XpRoleRewards");

            migrationBuilder.DropTable(
                name: "XpRoleTracking");

            migrationBuilder.DropTable(
                name: "XpUserSnapshots");

            migrationBuilder.DropIndex(
                name: "IX_Template_GuildId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateBarId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateClubId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateGuildId",
                table: "Template");

            migrationBuilder.DropIndex(
                name: "IX_Template_TemplateUserId",
                table: "Template");

            migrationBuilder.AlterColumn<DateTime>(
                name: "LastLevelUp",
                table: "UserXpStats",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(2017, 9, 21, 20, 53, 13, 307, DateTimeKind.Local),
                oldClrType: typeof(DateTime),
                oldType: "timestamp without time zone");

            migrationBuilder.CreateTable(
                name: "XpSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    GuildConfigId = table.Column<int>(type: "integer", nullable: false),
                    NotifyMessage = table.Column<string>(type: "text", nullable: true),
                    ServerExcluded = table.Column<bool>(type: "boolean", nullable: false),
                    XpRoleRewardExclusive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpSettings_GuildConfigs_GuildConfigId",
                        column: x => x.GuildConfigId,
                        principalTable: "GuildConfigs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ExcludedItem",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ItemId = table.Column<decimal>(type: "numeric(20,0)", nullable: false),
                    ItemType = table.Column<int>(type: "integer", nullable: false),
                    XpSettingsId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExcludedItem", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExcludedItem_XpSettings_XpSettingsId",
                        column: x => x.XpSettingsId,
                        principalTable: "XpSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XpCurrencyReward",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    XpSettingsId = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpCurrencyReward", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpCurrencyReward_XpSettings_XpSettingsId",
                        column: x => x.XpSettingsId,
                        principalTable: "XpSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "XpRoleReward",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    XpSettingsId = table.Column<int>(type: "integer", nullable: false),
                    DateAdded = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    RoleId = table.Column<decimal>(type: "numeric(20,0)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_XpRoleReward", x => x.Id);
                    table.ForeignKey(
                        name: "FK_XpRoleReward_XpSettings_XpSettingsId",
                        column: x => x.XpSettingsId,
                        principalTable: "XpSettings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_AwardedXp",
                table: "UserXpStats",
                column: "AwardedXp");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_GuildId",
                table: "UserXpStats",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_UserId",
                table: "UserXpStats",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_UserId_GuildId",
                table: "UserXpStats",
                columns: new[] { "UserId", "GuildId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserXpStats_Xp",
                table: "UserXpStats",
                column: "Xp");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateBarId",
                table: "Template",
                column: "TemplateBarId");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateClubId",
                table: "Template",
                column: "TemplateClubId");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateGuildId",
                table: "Template",
                column: "TemplateGuildId");

            migrationBuilder.CreateIndex(
                name: "IX_Template_TemplateUserId",
                table: "Template",
                column: "TemplateUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ExcludedItem_XpSettingsId",
                table: "ExcludedItem",
                column: "XpSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_XpCurrencyReward_XpSettingsId",
                table: "XpCurrencyReward",
                column: "XpSettingsId");

            migrationBuilder.CreateIndex(
                name: "IX_XpRoleReward_XpSettingsId_Level",
                table: "XpRoleReward",
                columns: new[] { "XpSettingsId", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_XpSettings_GuildConfigId",
                table: "XpSettings",
                column: "GuildConfigId",
                unique: true);
        }
    }
}
