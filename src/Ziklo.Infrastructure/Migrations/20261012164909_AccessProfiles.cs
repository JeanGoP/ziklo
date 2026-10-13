using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AccessProfiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ProfileId",
                table: "UserCompanies",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AccessProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PermissionMask = table.Column<long>(type: "bigint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccessProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccessProfiles_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_ProfileId",
                table: "UserCompanies",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_AccessProfiles_CompanyId_Name",
                table: "AccessProfiles",
                columns: new[] { "CompanyId", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_UserCompanies_AccessProfiles_ProfileId",
                table: "UserCompanies",
                column: "ProfileId",
                principalTable: "AccessProfiles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_UserCompanies_AccessProfiles_ProfileId",
                table: "UserCompanies");

            migrationBuilder.DropTable(
                name: "AccessProfiles");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanies_ProfileId",
                table: "UserCompanies");

            migrationBuilder.DropColumn(
                name: "ProfileId",
                table: "UserCompanies");
        }
    }
}
