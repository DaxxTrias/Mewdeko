﻿using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mewdeko.Database.Migrations.PostgreSql
{
    /// <inheritdoc />
    public partial class MessageCountsAddition2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Clear all existing data from the MessageCounts table
            migrationBuilder.Sql("DELETE FROM \"MessageCounts\"");

            migrationBuilder.AddColumn<string>(
                name: "RecentTimestamps",
                table: "MessageCounts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RecentTimestamps",
                table: "MessageCounts");
        }
    }
}