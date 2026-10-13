using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreditApplications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreditApplications",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(35)", maxLength: 35, nullable: false),
                    ProductVersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IdentificationType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    IdentificationNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ApplicantName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    City = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    EconomicActivity = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    MonthlyIncome = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    MonthlyObligations = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RequestedAmount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    RequestedInstallments = table.Column<int>(type: "int", nullable: false),
                    Purpose = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AcknowledgementVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditApplications", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_CreditApplications_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditApplications_ProductVersions_CompanyId_ProductVersionId",
                        columns: x => new { x.CompanyId, x.ProductVersionId },
                        principalTable: "ProductVersions",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CreditApplicationDocuments",
                columns: table => new
                {
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApplicationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    Content = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CreditApplicationDocuments", x => new { x.CompanyId, x.Id });
                    table.ForeignKey(
                        name: "FK_CreditApplicationDocuments_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CreditApplicationDocuments_CreditApplications_CompanyId_ApplicationId",
                        columns: x => new { x.CompanyId, x.ApplicationId },
                        principalTable: "CreditApplications",
                        principalColumns: new[] { "CompanyId", "Id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplicationDocuments_CompanyId_ApplicationId_Kind",
                table: "CreditApplicationDocuments",
                columns: new[] { "CompanyId", "ApplicationId", "Kind" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_CompanyId_CreatedAtUtc",
                table: "CreditApplications",
                columns: new[] { "CompanyId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_CompanyId_IdentificationNumber",
                table: "CreditApplications",
                columns: new[] { "CompanyId", "IdentificationNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_CompanyId_ProductVersionId",
                table: "CreditApplications",
                columns: new[] { "CompanyId", "ProductVersionId" });

            migrationBuilder.CreateIndex(
                name: "IX_CreditApplications_CompanyId_Reference",
                table: "CreditApplications",
                columns: new[] { "CompanyId", "Reference" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CreditApplicationDocuments");

            migrationBuilder.DropTable(
                name: "CreditApplications");
        }
    }
}
