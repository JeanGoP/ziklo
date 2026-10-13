using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaymentReceipts",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Received = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Applied = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Credit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReceipts", x => new { x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_PaymentReceipts_CompanyId_LoanId_Id", x => new { x.CompanyId, x.LoanId, x.Id });
                    table.CheckConstraint("CK_PaymentReceipt_Amounts", "[Received] > 0 AND [Applied] >= 0 AND [Credit] >= 0 AND [Received] = [Applied] + [Credit]");
                    table.ForeignKey(
                        name: "FK_PaymentReceipts_Clients_CompanyId_ClientId",
                        columns: x => new { x.CompanyId, x.ClientId },
                        principalTable: "Clients",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReceipts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReceipts_LoanEvents_CompanyId_LoanId_EventId",
                        columns: x => new { x.CompanyId, x.LoanId, x.EventId },
                        principalTable: "LoanEvents",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReceipts_Loans_CompanyId_LoanId",
                        columns: x => new { x.CompanyId, x.LoanId },
                        principalTable: "Loans",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReceipts_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentAllocations",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentAllocations", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_PaymentAllocation_Amount", "[Amount] > 0");
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_LoanInstallments_CompanyId_LoanId_InstallmentId",
                        columns: x => new { x.CompanyId, x.LoanId, x.InstallmentId },
                        principalTable: "LoanInstallments",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentAllocations_PaymentReceipts_CompanyId_LoanId_ReceiptId",
                        columns: x => new { x.CompanyId, x.LoanId, x.ReceiptId },
                        principalTable: "PaymentReceipts",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CompanyId_LoanId_InstallmentId",
                table: "PaymentAllocations",
                columns: new[] { "CompanyId", "LoanId", "InstallmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CompanyId_LoanId_ReceiptId",
                table: "PaymentAllocations",
                columns: new[] { "CompanyId", "LoanId", "ReceiptId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentAllocations_CompanyId_ReceiptId_Order",
                table: "PaymentAllocations",
                columns: new[] { "CompanyId", "ReceiptId", "Order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_ActorUserId",
                table: "PaymentReceipts",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_CompanyId_ClientId",
                table: "PaymentReceipts",
                columns: new[] { "CompanyId", "ClientId" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_CompanyId_EffectiveDate_Status",
                table: "PaymentReceipts",
                columns: new[] { "CompanyId", "EffectiveDate", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_CompanyId_IdempotencyKey",
                table: "PaymentReceipts",
                columns: new[] { "CompanyId", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReceipts_CompanyId_LoanId_EventId",
                table: "PaymentReceipts",
                columns: new[] { "CompanyId", "LoanId", "EventId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaymentAllocations");

            migrationBuilder.DropTable(
                name: "PaymentReceipts");
        }
    }
}
