using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreditApplicationLoanLink : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LoanId",
                table: "CreditApplications",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_CompanyId_LoanId",
                table: "CreditApplications",
                columns: new[] { "CompanyId", "LoanId" },
                unique: true,
                filter: "[LoanId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_CreditApplications_Loans_CompanyId_LoanId",
                table: "CreditApplications",
                columns: new[] { "CompanyId", "LoanId" },
                principalTable: "Loans",
                principalColumns: new[] { "CompanyId", "Id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_CreditApplications_Loans_CompanyId_LoanId",
                table: "CreditApplications");

            migrationBuilder.DropIndex(
                name: "IX_CreditApplications_CompanyId_LoanId",
                table: "CreditApplications");

            migrationBuilder.DropColumn(
                name: "LoanId",
                table: "CreditApplications");
        }
    }
}
