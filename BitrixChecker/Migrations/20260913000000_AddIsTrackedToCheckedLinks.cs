using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BitrixChecker.Migrations
{
    public partial class AddIsTrackedToCheckedLinks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTracked",
                table: "CheckedLinks",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsTracked",
                table: "CheckedLinks");
        }
    }
}
