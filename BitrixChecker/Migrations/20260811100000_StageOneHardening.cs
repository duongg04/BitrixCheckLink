using BitrixChecker.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitrixChecker.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260811100000_StageOneHardening")]
public partial class StageOneHardening : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // The CheckedLinks.Status column was created as LONGTEXT by InitialCreate, but the model
        // declares it as a bounded string (MaxLength(20) => varchar(20)). MySQL cannot create a
        // normal index on a TEXT/BLOB column without a key length, so IX_CheckedLinks_Status_LastChecked
        // could never be applied (ERROR 1170) and the whole migration aborted before the
        // __EFMigrationsHistory row was written. Align the column to the model first so the
        // migration is actually runnable on MySQL.
        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "CheckedLinks",
            type: "varchar(20)",
            maxLength: 20,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: false);

        migrationBuilder.AddColumn<string>(name: "UpdatedByUserId", table: "LinkProcessings", type: "varchar(255)", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_CheckedLinks_Status_LastChecked", table: "CheckedLinks", columns: ["Status", "LastChecked"]);
        migrationBuilder.CreateIndex(name: "IX_LinkProcessings_AssignedUserId_Status_UpdatedAt", table: "LinkProcessings", columns: ["AssignedUserId", "Status", "UpdatedAt"]);
        migrationBuilder.CreateIndex(name: "IX_LinkProcessings_UpdatedByUserId", table: "LinkProcessings", column: "UpdatedByUserId");
        migrationBuilder.AddForeignKey(name: "FK_LinkProcessings_AspNetUsers_UpdatedByUserId", table: "LinkProcessings", column: "UpdatedByUserId", principalTable: "AspNetUsers", principalColumn: "Id", onDelete: ReferentialAction.SetNull);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Reverse the Status column alteration (it was the first operation in Up, so it is the
        // last to be undone). Also drop the index that depended on it.
        migrationBuilder.DropIndex(name: "IX_CheckedLinks_Status_LastChecked", table: "CheckedLinks");

        migrationBuilder.AlterColumn<string>(
            name: "Status",
            table: "CheckedLinks",
            type: "longtext",
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(20)",
            oldMaxLength: 20);

        migrationBuilder.DropForeignKey(name: "FK_LinkProcessings_AspNetUsers_UpdatedByUserId", table: "LinkProcessings");
        migrationBuilder.DropIndex(name: "IX_LinkProcessings_AssignedUserId_Status_UpdatedAt", table: "LinkProcessings");
        migrationBuilder.DropIndex(name: "IX_LinkProcessings_UpdatedByUserId", table: "LinkProcessings");
        migrationBuilder.DropColumn(name: "UpdatedByUserId", table: "LinkProcessings");
    }
}
