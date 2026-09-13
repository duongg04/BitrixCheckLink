using BitrixChecker.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitrixChecker.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260906000000_MakeSaleStatusNullableOnCheckedLinks")]
public partial class MakeSaleStatusNullableOnCheckedLinks : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "SaleStatus",
            table: "CheckedLinks",
            type: "longtext",
            nullable: true,
            oldClrType: typeof(string),
            oldType: "longtext");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<string>(
            name: "SaleStatus",
            table: "CheckedLinks",
            type: "longtext",
            nullable: false,
            defaultValue: "",
            oldClrType: typeof(string),
            oldType: "longtext",
            oldNullable: true);
    }
}
