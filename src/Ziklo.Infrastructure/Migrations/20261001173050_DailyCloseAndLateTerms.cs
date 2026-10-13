using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DailyCloseAndLateTerms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "LateAnnualRate",
                table: "ProductVersions",
                type: "decimal(18,10)",
                precision: 18,
                scale: 10,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LateGraceDays",
                table: "ProductVersions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LateGraceMode",
                table: "ProductVersions",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Prospective");

            migrationBuilder.AddColumn<decimal>(
                name: "LateMaximumPerInstallment",
                table: "ProductVersions",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LateAnnualRate",
                table: "LoanTerms",
                type: "decimal(18,10)",
                precision: 18,
                scale: 10,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "LateGraceDays",
                table: "LoanTerms",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "LateGraceMode",
                table: "LoanTerms",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Prospective");

            migrationBuilder.AddColumn<decimal>(
                name: "LateMaximumPerInstallment",
                table: "LoanTerms",
                type: "decimal(19,4)",
                precision: 19,
                scale: 4,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "DailyCloseRuns",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoansProcessed = table.Column<int>(type: "int", nullable: false),
                    AccrualEvents = table.Column<int>(type: "int", nullable: false),
                    LateEvents = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyCloseRuns", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_DailyCloseRuns_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DailyCloseRuns_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LateInterestAssessments",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CutoffDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EligibleDays = table.Column<int>(type: "int", nullable: false),
                    BaseAtCutoff = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    AnnualRate = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    AlreadyPosted = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CumulativeInterest = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    AmountPosted = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    GraceDays = table.Column<int>(type: "int", nullable: false),
                    GraceMode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DayCount = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LateInterestAssessments", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_LateInterestAssessments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateInterestAssessments_LoanEvents_CompanyId_LoanId_EventId",
                        columns: x => new { x.CompanyId, x.LoanId, x.EventId },
                        principalTable: "LoanEvents",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LateInterestAssessments_LoanInstallments_CompanyId_LoanId_InstallmentId",
                        columns: x => new { x.CompanyId, x.LoanId, x.InstallmentId },
                        principalTable: "LoanInstallments",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseRuns_ActorUserId",
                table: "DailyCloseRuns",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_DailyCloseRuns_CompanyId_BusinessDate",
                table: "DailyCloseRuns",
                columns: new[] { "CompanyId", "BusinessDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LateInterestAssessments_CompanyId_LoanId_EventId",
                table: "LateInterestAssessments",
                columns: new[] { "CompanyId", "LoanId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_LateInterestAssessments_CompanyId_LoanId_InstallmentId_CutoffDate",
                table: "LateInterestAssessments",
                columns: new[] { "CompanyId", "LoanId", "InstallmentId", "CutoffDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyCloseRuns");

            migrationBuilder.DropTable(
                name: "LateInterestAssessments");

            migrationBuilder.DropColumn(
                name: "LateAnnualRate",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "LateGraceDays",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "LateGraceMode",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "LateMaximumPerInstallment",
                table: "ProductVersions");

            migrationBuilder.DropColumn(
                name: "LateAnnualRate",
                table: "LoanTerms");

            migrationBuilder.DropColumn(
                name: "LateGraceDays",
                table: "LoanTerms");

            migrationBuilder.DropColumn(
                name: "LateGraceMode",
                table: "LoanTerms");

            migrationBuilder.DropColumn(
                name: "LateMaximumPerInstallment",
                table: "LoanTerms");
        }
    }
}
