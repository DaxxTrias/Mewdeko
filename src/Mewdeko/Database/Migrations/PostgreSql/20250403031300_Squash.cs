using Microsoft.EntityFrameworkCore.Migrations;

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class Squash : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add GuildId column to all related tables

            // Anti-protection tables
            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "AntiRaidSetting",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "AntiSpamSetting",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "AntiMassMentionSetting",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "AntiAltSetting",
                type: "numeric(20,0)",
                nullable: true);

            // Role-related tables
            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "StreamRoleSettings",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "GroupName",
                type: "numeric(20,0)",
                nullable: true);

            // Command-related tables
            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "CommandAlias",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "CommandCooldown",
                type: "numeric(20,0)",
                nullable: true);

            // Filter tables
            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "FilteredWord",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "FilterWordsChannelIds",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "FilterInvitesChannelIds",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "FilterLinksChannelId",
                type: "numeric(20,0)",
                nullable: true);

            // Warning/Punishment tables
            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "WarningPunishment",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "WarningPunishment2",
                type: "numeric(20,0)",
                nullable: true);

            // Timer tables
            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "UnmuteTimer",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "UnbanTimer",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "UnroleTimer",
                type: "numeric(20,0)",
                nullable: true);

            // Other tables

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "MutedUserId",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "FeedSub",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "NsfwBlacklitedTag",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "DelMsgOnCmdChannel",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "ReactionRoleMessage",
                type: "numeric(20,0)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GuildId",
                table: "Permissionv2",
                type: "numeric(20,0)",
                nullable: true);

            // 2. Populate GuildId with data from GuildConfig

            // Anti-protection tables
            migrationBuilder.Sql(@"
                UPDATE ""AntiRaidSetting"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""AntiRaidSetting"".""GuildConfigId""
                );

                UPDATE ""AntiSpamSetting"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""AntiSpamSetting"".""GuildConfigId""
                );

                UPDATE ""AntiMassMentionSetting"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""AntiMassMentionSetting"".""GuildConfigId""
                );

                UPDATE ""AntiAltSetting"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""AntiAltSetting"".""GuildConfigId""
                );
            ");

            // Role-related tables
            migrationBuilder.Sql(@"
                UPDATE ""StreamRoleSettings"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""StreamRoleSettings"".""GuildConfigId""
                );

                UPDATE ""GroupName"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""GroupName"".""GuildConfigId""
                );
            ");

            // Command-related tables
            migrationBuilder.Sql(@"
                UPDATE ""CommandAlias"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""CommandAlias"".""GuildConfigId""
                );

                UPDATE ""CommandCooldown"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""CommandCooldown"".""GuildConfigId""
                );
            ");

            // Filter tables
            migrationBuilder.Sql(@"
                UPDATE ""FilteredWord"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""FilteredWord"".""GuildConfigId""
                );

                UPDATE ""FilterWordsChannelIds"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""FilterWordsChannelIds"".""GuildConfigId""
                );

                UPDATE ""FilterInvitesChannelIds"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""FilterInvitesChannelIds"".""GuildConfigId""
                );

                UPDATE ""FilterLinksChannelId"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""FilterLinksChannelId"".""GuildConfigId""
                );
            ");

            // Warning/Punishment tables
            migrationBuilder.Sql(@"
                UPDATE ""WarningPunishment"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""WarningPunishment"".""GuildConfigId""
                );

                UPDATE ""WarningPunishment2"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""WarningPunishment2"".""GuildConfigId""
                );
            ");

            // Timer tables
            migrationBuilder.Sql(@"
                UPDATE ""UnmuteTimer"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""UnmuteTimer"".""GuildConfigId""
                );

                UPDATE ""UnbanTimer"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""UnbanTimer"".""GuildConfigId""
                );

                UPDATE ""UnroleTimer"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""UnroleTimer"".""GuildConfigId""
                );
            ");

            // Other tables
            migrationBuilder.Sql(@"

                UPDATE ""MutedUserId"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""MutedUserId"".""GuildConfigId""
                );

                UPDATE ""FeedSub"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""FeedSub"".""GuildConfigId""
                );

                UPDATE ""NsfwBlacklitedTag"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""NsfwBlacklitedTag"".""GuildConfigId""
                );

                UPDATE ""DelMsgOnCmdChannel"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""DelMsgOnCmdChannel"".""GuildConfigId""
                );

                UPDATE ""ReactionRoleMessage"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""ReactionRoleMessage"".""GuildConfigId""
                );

                UPDATE ""Permissionv2"" SET ""GuildId"" = (
                    SELECT ""GuildId"" FROM ""GuildConfigs"" WHERE ""Id"" = ""Permissionv2"".""GuildConfigId""
                );
            ");

            // 3. Make GuildId non-nullable and create indexes

            // Anti-protection tables
            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "AntiRaidSetting",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "AntiSpamSetting",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "AntiMassMentionSetting",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "AntiAltSetting",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            // Role-related tables
            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "StreamRoleSettings",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "GroupName",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            // Command-related tables
            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "CommandAlias",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "CommandCooldown",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            // Filter tables
            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "FilteredWord",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "FilterWordsChannelIds",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "FilterInvitesChannelIds",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "FilterLinksChannelId",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            // Warning/Punishment tables
            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "WarningPunishment",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "WarningPunishment2",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            // Timer tables
            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "UnmuteTimer",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "UnbanTimer",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "UnroleTimer",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            // Other tables
            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "GuildRepeater",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "MutedUserId",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "FeedSub",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "FollowedStream",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "NsfwBlacklitedTag",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "DelMsgOnCmdChannel",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "ReactionRoleMessage",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "GuildId",
                table: "Permissionv2",
                type: "numeric(20,0)",
                nullable: false,
                defaultValue: 0m);

            // 4. Create indexes for the new GuildId columns

            migrationBuilder.CreateIndex(
                name: "IX_AntiRaidSetting_GuildId",
                table: "AntiRaidSetting",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntiSpamSetting_GuildId",
                table: "AntiSpamSetting",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntiMassMentionSetting_GuildId",
                table: "AntiMassMentionSetting",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AntiAltSetting_GuildId",
                table: "AntiAltSetting",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StreamRoleSettings_GuildId",
                table: "StreamRoleSettings",
                column: "GuildId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GroupName_GuildId_Number",
                table: "GroupName",
                columns: new[] { "GuildId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CommandAlias_GuildId_Trigger",
                table: "CommandAlias",
                columns: new[] { "GuildId", "Trigger" });

            migrationBuilder.CreateIndex(
                name: "IX_CommandCooldown_GuildId",
                table: "CommandCooldown",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_FilteredWord_GuildId",
                table: "FilteredWord",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_WarningPunishment2_GuildId_Count",
                table: "WarningPunishment2",
                columns: new[] { "GuildId", "Count" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnmuteTimer_GuildId_UserId",
                table: "UnmuteTimer",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnbanTimer_GuildId_UserId",
                table: "UnbanTimer",
                columns: new[] { "GuildId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_GuildRepeater_GuildId",
                table: "GuildRepeater",
                column: "GuildId");

            migrationBuilder.CreateIndex(
                name: "IX_FeedSub_GuildId_Url",
                table: "FeedSub",
                columns: new[] { "GuildId", "Url" },
                unique: true);

            // 5. Drop the old GuildConfigId columns

            // Anti-protection tables
            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "AntiRaidSetting");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "AntiSpamSetting");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "AntiMassMentionSetting");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "AntiAltSetting");

            // Role-related tables
            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "StreamRoleSettings");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "GroupName");

            // Command-related tables
            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "CommandAlias");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "CommandCooldown");

            // Filter tables
            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "FilteredWord");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "FilterWordsChannelIds");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "FilterInvitesChannelIds");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "FilterLinksChannelId");

            // Warning/Punishment tables
            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "WarningPunishment");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "WarningPunishment2");

            // Timer tables
            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "UnmuteTimer");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "UnbanTimer");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "UnroleTimer");

            // Other tables
            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "GuildRepeater");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "MutedUserId");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "FeedSub");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "FollowedStream");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "NsfwBlacklitedTag");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "DelMsgOnCmdChannel");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "ReactionRoleMessage");

            migrationBuilder.DropColumn(
                name: "GuildConfigId",
                table: "Permissionv2");

             migrationBuilder.RenameTable(
                name: "ReactionRoleMessage",
                newName: "ReactionRoleMessages");

            migrationBuilder.RenameTable(
                name: "Permissionv2",
                newName: "Permissions");

            migrationBuilder.RenameTable(
                name: "NsfwBlacklitedTag",
                newName: "NsfwBlacklistedTags");

            migrationBuilder.RenameTable(
                name: "FollowedStream",
                newName: "FollowedStreams");

            migrationBuilder.RenameTable(
                name: "FilterLinksChannelId",
                newName: "FilterLinksChannelIds");

            migrationBuilder.RenameTable(
                name: "DelMsgOnCmdChannel",
                newName: "DelMsgOnCmdChannels");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // No going back.
        }
    }
}