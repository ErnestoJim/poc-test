using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSpecificationLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ApprovedAtUtc",
                table: "Specifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "ImplementedAtUtc",
                table: "Specifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Specifications",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.AddColumn<long>(
                name: "VerifiedAtUtc",
                table: "Specifications",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "Specifications"
                SET "Status" = 'Draft',
                    "Version" = "Version" + 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ApprovedAtUtc",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "ImplementedAtUtc",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Specifications");

            migrationBuilder.DropColumn(
                name: "VerifiedAtUtc",
                table: "Specifications");
        }
    }
}
