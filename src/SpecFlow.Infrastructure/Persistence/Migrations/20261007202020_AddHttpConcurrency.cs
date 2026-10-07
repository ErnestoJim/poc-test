using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddHttpConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "Specifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Version",
                table: "Specifications");
        }
    }
}
