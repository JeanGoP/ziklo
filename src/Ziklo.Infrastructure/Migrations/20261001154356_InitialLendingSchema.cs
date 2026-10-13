using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialLendingSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Companies",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TaxIdentifier = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    TimeZoneId = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Companies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Clients",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentificationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    WhatsApp = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    BirthDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EconomicActivity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    MonthlyIncome = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    Employer = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Clients", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_Clients_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditProducts",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditProducts", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_CreditProducts_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProductVersions",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    MinimumAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    MaximumAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    MinimumInstallments = table.Column<int>(type: "int", nullable: false),
                    MaximumInstallments = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    RateKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DayCount = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CurrencyDecimals = table.Column<int>(type: "int", nullable: false),
                    AllowsCapitalization = table.Column<bool>(type: "bit", nullable: false),
                    InsurancePerInstallment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OtherChargePerInstallment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductVersions", x => new { x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_ProductVersions_CompanyId_ProductId_Id", x => new { x.CompanyId, x.ProductId, x.Id });
                    table.CheckConstraint("CK_ProductVersion_Limits", "[MinimumAmount] > 0 AND [MaximumAmount] >= [MinimumAmount] AND [MinimumInstallments] > 0 AND [MaximumInstallments] >= [MinimumInstallments] AND [Rate] >= 0");
                    table.ForeignKey(
                        name: "FK_ProductVersions_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProductVersions_CreditProducts_CompanyId_ProductId",
                        columns: x => new { x.CompanyId, x.ProductId },
                        principalTable: "CreditProducts",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Loans",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProductVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RequestDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ApprovalDate = table.Column<DateOnly>(type: "date", nullable: true),
                    DisbursementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    FirstDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalPrincipal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RequestedInstallments = table.Column<int>(type: "int", nullable: false),
                    RequestedCapitalization = table.Column<bool>(type: "bit", nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Version = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Loans", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_Loan_Principal", "[OriginalPrincipal] > 0");
                    table.ForeignKey(
                        name: "FK_Loans_Clients_CompanyId_ClientId",
                        columns: x => new { x.CompanyId, x.ClientId },
                        principalTable: "Clients",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Loans_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Loans_CreditProducts_CompanyId_ProductId",
                        columns: x => new { x.CompanyId, x.ProductId },
                        principalTable: "CreditProducts",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Loans_ProductVersions_CompanyId_ProductId_ProductVersionId",
                        columns: x => new { x.CompanyId, x.ProductId, x.ProductVersionId },
                        principalTable: "ProductVersions",
                        principalColumns: new[] { "CompanyId", "ProductId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanEvents",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    EffectiveDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OriginReference = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    ReversesEventId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanEvents", x => new { x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_LoanEvents_CompanyId_LoanId_Id", x => new { x.CompanyId, x.LoanId, x.Id });
                    table.ForeignKey(
                        name: "FK_LoanEvents_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanEvents_Loans_CompanyId_LoanId",
                        columns: x => new { x.CompanyId, x.LoanId },
                        principalTable: "Loans",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanPlans",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    IsCurrent = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    PreviousPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanPlans", x => new { x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_LoanPlans_CompanyId_LoanId_Id", x => new { x.CompanyId, x.LoanId, x.Id });
                    table.ForeignKey(
                        name: "FK_LoanPlans_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanPlans_Loans_CompanyId_LoanId",
                        columns: x => new { x.CompanyId, x.LoanId },
                        principalTable: "Loans",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanTerms",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceProductVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,10)", precision: 18, scale: 10, nullable: false),
                    RateKind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    DayCount = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Method = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Installments = table.Column<int>(type: "int", nullable: false),
                    CurrencyDecimals = table.Column<int>(type: "int", nullable: false),
                    CapitalizeInterest = table.Column<bool>(type: "bit", nullable: false),
                    InsurancePerInstallment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OtherChargePerInstallment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CapturedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanTerms", x => new { x.CompanyId, x.LoanId });
                    table.ForeignKey(
                        name: "FK_LoanTerms_Loans_CompanyId_LoanId",
                        columns: x => new { x.CompanyId, x.LoanId },
                        principalTable: "Loans",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanTerms_ProductVersions_CompanyId_SourceProductVersionId",
                        columns: x => new { x.CompanyId, x.SourceProductVersionId },
                        principalTable: "ProductVersions",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanInstallments",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    OpeningPrincipal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Principal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Interest = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CapitalizedInterest = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Insurance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    OtherCharges = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    Payment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    ClosingPrincipal = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanInstallments", x => new { x.CompanyId, x.Id });
                    table.UniqueConstraint("AK_LoanInstallments_CompanyId_LoanId_Id", x => new { x.CompanyId, x.LoanId, x.Id });
                    table.ForeignKey(
                        name: "FK_LoanInstallments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanInstallments_LoanPlans_CompanyId_LoanId_PlanId",
                        columns: x => new { x.CompanyId, x.LoanId, x.PlanId },
                        principalTable: "LoanPlans",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LoanMovements",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LoanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InstallmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: false),
                    Component = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "nchar(3)", fixedLength: true, maxLength: 3, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoanMovements", x => new { x.CompanyId, x.Id });
                    table.CheckConstraint("CK_LoanMovement_Amount", "[Amount] <> 0");
                    table.ForeignKey(
                        name: "FK_LoanMovements_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanMovements_LoanEvents_CompanyId_LoanId_EventId",
                        columns: x => new { x.CompanyId, x.LoanId, x.EventId },
                        principalTable: "LoanEvents",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LoanMovements_LoanInstallments_CompanyId_LoanId_InstallmentId",
                        columns: x => new { x.CompanyId, x.LoanId, x.InstallmentId },
                        principalTable: "LoanInstallments",
                        principalColumns: new[] { "CompanyId", "LoanId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Clients_CompanyId_IdentificationType_IdentificationNumber",
                table: "Clients",
                columns: new[] { "CompanyId", "IdentificationType", "IdentificationNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Companies_TaxIdentifier",
                table: "Companies",
                column: "TaxIdentifier",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditProducts_CompanyId_Code",
                table: "CreditProducts",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanEvents_CompanyId_Type_IdempotencyKey",
                table: "LoanEvents",
                columns: new[] { "CompanyId", "Type", "IdempotencyKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanInstallments_CompanyId_DueDate",
                table: "LoanInstallments",
                columns: new[] { "CompanyId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanInstallments_CompanyId_LoanId_PlanId",
                table: "LoanInstallments",
                columns: new[] { "CompanyId", "LoanId", "PlanId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanInstallments_CompanyId_PlanId_Number",
                table: "LoanInstallments",
                columns: new[] { "CompanyId", "PlanId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanMovements_CompanyId_LoanId_EventId",
                table: "LoanMovements",
                columns: new[] { "CompanyId", "LoanId", "EventId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanMovements_CompanyId_LoanId_InstallmentId",
                table: "LoanMovements",
                columns: new[] { "CompanyId", "LoanId", "InstallmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanMovements_CompanyId_LoanId_Sequence",
                table: "LoanMovements",
                columns: new[] { "CompanyId", "LoanId", "Sequence" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LoanPlans_CompanyId_LoanId_IsCurrent",
                table: "LoanPlans",
                columns: new[] { "CompanyId", "LoanId", "IsCurrent" },
                unique: true,
                filter: "[IsCurrent] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_LoanPlans_CompanyId_LoanId_Number",
                table: "LoanPlans",
                columns: new[] { "CompanyId", "LoanId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_CompanyId_ClientId_Status",
                table: "Loans",
                columns: new[] { "CompanyId", "ClientId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Loans_CompanyId_Number",
                table: "Loans",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Loans_CompanyId_ProductId_ProductVersionId",
                table: "Loans",
                columns: new[] { "CompanyId", "ProductId", "ProductVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_LoanTerms_CompanyId_SourceProductVersionId",
                table: "LoanTerms",
                columns: new[] { "CompanyId", "SourceProductVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductVersions_CompanyId_ProductId_Number",
                table: "ProductVersions",
                columns: new[] { "CompanyId", "ProductId", "Number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoanMovements");

            migrationBuilder.DropTable(
                name: "LoanTerms");

            migrationBuilder.DropTable(
                name: "LoanEvents");

            migrationBuilder.DropTable(
                name: "LoanInstallments");

            migrationBuilder.DropTable(
                name: "LoanPlans");

            migrationBuilder.DropTable(
                name: "Loans");

            migrationBuilder.DropTable(
                name: "Clients");

            migrationBuilder.DropTable(
                name: "ProductVersions");

            migrationBuilder.DropTable(
                name: "CreditProducts");

            migrationBuilder.DropTable(
                name: "Companies");
        }
    }
}
