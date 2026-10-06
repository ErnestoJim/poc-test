using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureProposalLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "DecidedAtUtc",
                table: "FeatureProposals",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "FeatureProposals",
                type: "TEXT",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "FeatureProposals",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "FeatureProposals",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DecidedAtUtc",
                table: "FeatureProposals");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "FeatureProposals");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "FeatureProposals");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "FeatureProposals");
        }
    }
}
