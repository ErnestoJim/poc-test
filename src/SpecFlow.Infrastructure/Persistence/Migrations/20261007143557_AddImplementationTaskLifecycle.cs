using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImplementationTaskLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "CompletedAtUtc",
                table: "ImplementationTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "StartedAtUtc",
                table: "ImplementationTasks",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ImplementationTasks",
                type: "TEXT",
                maxLength: 32,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompletedAtUtc",
                table: "ImplementationTasks");

            migrationBuilder.DropColumn(
                name: "StartedAtUtc",
                table: "ImplementationTasks");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ImplementationTasks");
        }
    }
}
