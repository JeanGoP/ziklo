using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Ziklo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EventActorRelation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LoanEvents_ActorUserId",
                table: "LoanEvents",
                column: "ActorUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_LoanEvents_Users_ActorUserId",
                table: "LoanEvents",
                column: "ActorUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LoanEvents_Users_ActorUserId",
                table: "LoanEvents");

            migrationBuilder.DropIndex(
                name: "IX_LoanEvents_ActorUserId",
                table: "LoanEvents");
        }
    }
}
