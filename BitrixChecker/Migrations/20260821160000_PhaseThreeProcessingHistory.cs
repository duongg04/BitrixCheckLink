using System;
using BitrixChecker.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitrixChecker.Migrations;

/// <inheritdoc />
[DbContext(typeof(AppDbContext))]
[Migration("20260821160000_PhaseThreeProcessingHistory")]
public partial class PhaseThreeProcessingHistory : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ProcessingHistories",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                LinkProcessingId = table.Column<int>(type: "int", nullable: false),
                AssignedUserId = table.Column<string>(type: "varchar(255)", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Status = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                ChangedByUserId = table.Column<string>(type: "varchar(255)", nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                ChangeReason = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProcessingHistories", x => x.Id);
                table.ForeignKey(
                    name: "FK_ProcessingHistories_AspNetUsers_AssignedUserId",
                    column: x => x.AssignedUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ProcessingHistories_AspNetUsers_ChangedByUserId",
                    column: x => x.ChangedByUserId,
                    principalTable: "AspNetUsers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_ProcessingHistories_LinkProcessings_LinkProcessingId",
                    column: x => x.LinkProcessingId,
                    principalTable: "LinkProcessings",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_ProcessingHistories_AssignedUserId",
            table: "ProcessingHistories",
            column: "AssignedUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProcessingHistories_ChangedAt",
            table: "ProcessingHistories",
            column: "ChangedAt");

        migrationBuilder.CreateIndex(
            name: "IX_ProcessingHistories_ChangedByUserId",
            table: "ProcessingHistories",
            column: "ChangedByUserId");

        migrationBuilder.CreateIndex(
            name: "IX_ProcessingHistories_LinkProcessingId",
            table: "ProcessingHistories",
            column: "LinkProcessingId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProcessingHistories");
    }
}
