using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace LedgerPay.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBackOffice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "RestrictedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "RestrictedByUserId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RestrictedReason",
                table: "Users",
                type: "nvarchar(250)",
                maxLength: 250,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_RestrictedByUserId",
                table: "Users",
                column: "RestrictedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions",
                column: "CreatedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs",
                columns: new[] { "Action", "CreatedAt" },
                descending: new[] { false, true });

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Users_RestrictedByUserId",
                table: "Users",
                column: "RestrictedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Users_RestrictedByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_RestrictedByUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_CreatedAt",
                table: "Transactions");

            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action_CreatedAt",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "RestrictedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RestrictedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RestrictedReason",
                table: "Users");
        }
    }
}
