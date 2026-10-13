using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CollectionsAndPromises : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PromisesEvaluated",
                table: "DailyCloseRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "CollectionActivities",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContactDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Result = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    NextContactDate = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionActivities", x => new { x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_CollectionActivities_CompanyId_LoanId_Id", x => new { x.CompanyId, x.LoanId, x.Id });
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Clients_CompanyId_ClientId",
                        columns: x => new { x.CompanyId, x.ClientId },
                        principalTable: "Clients",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Loans_CompanyId_LoanId",
                        columns: x => new { x.CompanyId, x.LoanId },
                        principalTable: "Loans",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionActivities_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentPromises",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionActivityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PromisedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    EvaluatedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentPromises", x => new { x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_PaymentPromises_CompanyId_LoanId_Id", x => new { x.CompanyId, x.LoanId, x.Id });
                    table.CheckConstraint("CK_PaymentPromise_Amount", "[PromisedAmount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentPromises_CollectionActivities_CompanyId_LoanId_CollectionActivityId",
                        columns: x => new { x.CompanyId, x.LoanId, x.CollectionActivityId },
                        principalTable: "CollectionActivities",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentPromises_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PromiseEvaluations",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PromiseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DailyCloseRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ReceivedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromiseEvaluations", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_PromiseEvaluations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromiseEvaluations_DailyCloseRuns_CompanyId_DailyCloseRunId",
                        columns: x => new { x.CompanyId, x.DailyCloseRunId },
                        principalTable: "DailyCloseRuns",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromiseEvaluations_PaymentPromises_CompanyId_LoanId_PromiseId",
                        columns: x => new { x.CompanyId, x.LoanId, x.PromiseId },
                        principalTable: "PaymentPromises",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PromiseEvaluations_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_ActorUserId",
                table: "CollectionActivities",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_CompanyId_ClientId",
                table: "CollectionActivities",
                columns: new[] { "CompanyId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_CollectionActivities_CompanyId_LoanId_ContactDate",
                table: "CollectionActivities",
                columns: new[] { "CompanyId", "LoanId", "ContactDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPromises_CompanyId_DueDate_Status",
                table: "PaymentPromises",
                columns: new[] { "CompanyId", "DueDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPromises_CompanyId_LoanId_CollectionActivityId",
                table: "PaymentPromises",
                columns: new[] { "CompanyId", "LoanId", "CollectionActivityId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentPromises_CompanyId_LoanId_Status",
                table: "PaymentPromises",
                columns: new[] { "CompanyId", "LoanId", "Status" },
                unique: true,
                filter: "[Status] = 'Pending'");

            migrationBuilder.CreateIndex(
                name: "IX_PromiseEvaluations_ActorUserId",
                table: "PromiseEvaluations",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PromiseEvaluations_CompanyId_DailyCloseRunId",
                table: "PromiseEvaluations",
                columns: new[] { "CompanyId", "DailyCloseRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromiseEvaluations_CompanyId_LoanId_PromiseId",
                table: "PromiseEvaluations",
                columns: new[] { "CompanyId", "LoanId", "PromiseId" });

            migrationBuilder.CreateIndex(
                name: "IX_PromiseEvaluations_CompanyId_PromiseId",
                table: "PromiseEvaluations",
                columns: new[] { "CompanyId", "PromiseId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromiseEvaluations");

            migrationBuilder.DropTable(
                name: "PaymentPromises");

            migrationBuilder.DropTable(
                name: "CollectionActivities");

            migrationBuilder.DropColumn(
                name: "PromisesEvaluated",
                table: "DailyCloseRuns");
        }
    }
}
