using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // EF Core generates inline arrays for composite indexes.

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddImplementationTasks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ImplementationTasksVersion",
                table: "Specifications",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ImplementationTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SpecificationId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    NormalizedTitle = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 10000, nullable: true),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    UpdatedAtUtc = table.Column<long>(type: "INTEGER", nullable: false),
                    Version = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImplementationTasks", x => x.Id);
                    table.CheckConstraint("CK_ImplementationTasks_Position_Positive", "\"Position\" > 0");
                    table.ForeignKey(
                        name: "FK_ImplementationTasks_Specifications_SpecificationId",
                        column: x => x.SpecificationId,
                        principalTable: "Specifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImplementationTasks_SpecificationId_NormalizedTitle",
                table: "ImplementationTasks",
                columns: new[] { "SpecificationId", "NormalizedTitle" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImplementationTasks_SpecificationId_Position",
                table: "ImplementationTasks",
                columns: new[] { "SpecificationId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImplementationTasks");

            migrationBuilder.DropColumn(
                name: "ImplementationTasksVersion",
                table: "Specifications");
        }
    }
}

#pragma warning restore CA1861
