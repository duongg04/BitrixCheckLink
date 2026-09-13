using System;
using BitrixChecker.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitrixChecker.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260825161442_SynchronizeCurrentSchema")]
public partial class SynchronizeCurrentSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "DeletedAt",
            table: "CheckedLinks",
            type: "datetime(6)",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DeletedAt",
            table: "CheckedLinks");
    }
}
