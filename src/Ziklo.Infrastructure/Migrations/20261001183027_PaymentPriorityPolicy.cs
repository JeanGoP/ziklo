using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentPriorityPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PaymentPriority",
                table: "ProductVersions",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "Charges,LateInterest,OverdueCurrentInterest,OverduePrincipal,CurrentInterest,Principal");

            migrationBuilder.AddColumn<string>(
                name: "PaymentPriority",
                table: "LoanTerms",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "Charges,LateInterest,OverdueCurrentInterest,OverduePrincipal,CurrentInterest,Principal");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaymentPriority",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "PaymentPriority",
                table: "LoanTerms");
        }
    }
}
