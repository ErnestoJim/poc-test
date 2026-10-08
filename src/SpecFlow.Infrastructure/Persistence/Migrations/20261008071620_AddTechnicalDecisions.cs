using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnicalDecisions : Migration
    {
        private static readonly string[] TechnicalDecisionOrderColumns =
            ["ProjectId", "CreatedAtUtc", "Id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TechnicalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ProjectId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Content = table.Column<string>(type: "TEXT", maxLength: 50000, nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnicalDecisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnicalDecisions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TechnicalDecisions_ProjectId_CreatedAtUtc_Id",
                table: "TechnicalDecisions",
                columns: TechnicalDecisionOrderColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnicalDecisions");
        }
    }
}
