using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ziklo.Infrastructure.Migrations;

[DbContext(typeof(ZikloDbContext))]
[Migration("20261002000000_ApplicationFormDetails")]
public sealed class ApplicationFormDetails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder) =>
        migrationBuilder.AddColumn<string>(
            name: "FormDataJson", table: "CreditApplications",
            type: "nvarchar(max)", nullable: false, defaultValue: "{}");

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropColumn(name: "FormDataJson", table: "CreditApplications");
}
