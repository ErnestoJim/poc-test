using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAcceptanceCriterionTaskTraceability : Migration
    {
        private static readonly string[] InverseTraceabilityColumns =
            ["AcceptanceCriterionId", "ImplementationTaskId"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AcceptanceCriteriaVersion",
                table: "ImplementationTasks",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ImplementationTaskAcceptanceCriteria",
                columns: table => new
                {
                    ImplementationTaskId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AcceptanceCriterionId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImplementationTaskAcceptanceCriteria", x => new { x.ImplementationTaskId, x.AcceptanceCriterionId });
                    table.ForeignKey(
                        name: "FK_ImplementationTaskAcceptanceCriteria_AcceptanceCriteria_AcceptanceCriterionId",
                        column: x => x.AcceptanceCriterionId,
                        principalTable: "AcceptanceCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImplementationTaskAcceptanceCriteria_ImplementationTasks_ImplementationTaskId",
                        column: x => x.ImplementationTaskId,
                        principalTable: "ImplementationTasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImplementationTaskAcceptanceCriteria_AcceptanceCriterionId_ImplementationTaskId",
                table: "ImplementationTaskAcceptanceCriteria",
                columns: InverseTraceabilityColumns);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImplementationTaskAcceptanceCriteria");

            migrationBuilder.DropColumn(
                name: "AcceptanceCriteriaVersion",
                table: "ImplementationTasks");
        }
    }
}
