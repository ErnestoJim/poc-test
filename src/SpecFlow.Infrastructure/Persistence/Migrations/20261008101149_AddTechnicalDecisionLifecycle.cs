using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalDecisionLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DecidedAtUtc",
                table: "TechnicalDecisions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "TechnicalDecisions",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "TechnicalDecisions",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<long>(
                name: "SupersededAtUtc",
                table: "TechnicalDecisions",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededByDecisionId",
                table: "TechnicalDecisions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "TechnicalDecisions"
                SET "Status" = 'Draft',
                    "Version" = "Version" + 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalDecisions_SupersededByDecisionId",
                table: "TechnicalDecisions",
                column: "SupersededByDecisionId");

            migrationBuilder.AddForeignKey(
                name: "FK_TechnicalDecisions_TechnicalDecisions_SupersededByDecisionId",
                table: "TechnicalDecisions",
                column: "SupersededByDecisionId",
                principalTable: "TechnicalDecisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TechnicalDecisions_TechnicalDecisions_SupersededByDecisionId",
                table: "TechnicalDecisions");

            migrationBuilder.DropIndex(
                name: "IX_TechnicalDecisions_SupersededByDecisionId",
                table: "TechnicalDecisions");

            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                table: "TechnicalDecisions");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "TechnicalDecisions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "TechnicalDecisions");

            migrationBuilder.DropColumn(
                name: "SupersededAtUtc",
                table: "TechnicalDecisions");

            migrationBuilder.DropColumn(
                name: "SupersededByDecisionId",
                table: "TechnicalDecisions");
        }
    }
}
