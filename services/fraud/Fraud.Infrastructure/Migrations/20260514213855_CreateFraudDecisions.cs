using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Fraud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateFraudDecisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FraudDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false),
                    Decision = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RulesTriggeredJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FraudDecisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FraudDecisions_CustomerEmail_CreatedAtUtc",
                table: "FraudDecisions",
                columns: new[] { "CustomerEmail", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FraudDecisions_PaymentId",
                table: "FraudDecisions",
                column: "PaymentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FraudDecisions");
        }
    }
}
