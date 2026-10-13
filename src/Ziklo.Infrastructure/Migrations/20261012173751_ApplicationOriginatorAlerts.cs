using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationOriginatorAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SubmittedByUserId",
                table: "CreditApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_CompanyId_SubmittedByUserId_Status",
                table: "CreditApplications",
                columns: new[] { "CompanyId", "SubmittedByUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_SubmittedByUserId",
                table: "CreditApplications",
                column: "SubmittedByUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditApplications_Users_SubmittedByUserId",
                table: "CreditApplications",
                column: "SubmittedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditApplications_Users_SubmittedByUserId",
                table: "CreditApplications");

            migrationBuilder.DropIndex(
                name: "IX_CreditApplications_CompanyId_SubmittedByUserId_Status",
                table: "CreditApplications");

            migrationBuilder.DropIndex(
                name: "IX_CreditApplications_SubmittedByUserId",
                table: "CreditApplications");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "CreditApplications");
        }
    }
}
