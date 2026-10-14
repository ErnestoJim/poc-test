using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptanceCriterionVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CheckedAtUtc",
                table: "AcceptanceCriteria",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationEvidence",
                table: "AcceptanceCriteria",
                type: "TEXT",
                maxLength: 10000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerificationStatus",
                table: "AcceptanceCriteria",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: "Unverified");

            migrationBuilder.AddColumn<int>(
                name: "VerificationVersion",
                table: "AcceptanceCriteria",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CheckedAtUtc",
                table: "AcceptanceCriteria");

            migrationBuilder.DropColumn(
                name: "VerificationEvidence",
                table: "AcceptanceCriteria");

            migrationBuilder.DropColumn(
                name: "VerificationStatus",
                table: "AcceptanceCriteria");

            migrationBuilder.DropColumn(
                name: "VerificationVersion",
                table: "AcceptanceCriteria");
        }
    }
}
