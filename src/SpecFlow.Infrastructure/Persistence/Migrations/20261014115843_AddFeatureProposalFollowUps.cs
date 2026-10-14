using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SpecFlow.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureProposalFollowUps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SourceProposalId",
                table: "FeatureProposals",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_FeatureProposals_SourceProposalId",
                table: "FeatureProposals",
                column: "SourceProposalId");

            migrationBuilder.AddForeignKey(
                name: "FK_FeatureProposals_FeatureProposals_SourceProposalId",
                table: "FeatureProposals",
                column: "SourceProposalId",
                principalTable: "FeatureProposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_FeatureProposals_FeatureProposals_SourceProposalId",
                table: "FeatureProposals");

            migrationBuilder.DropIndex(
                name: "IX_FeatureProposals_SourceProposalId",
                table: "FeatureProposals");

            migrationBuilder.DropColumn(
                name: "SourceProposalId",
                table: "FeatureProposals");
        }
    }
}
