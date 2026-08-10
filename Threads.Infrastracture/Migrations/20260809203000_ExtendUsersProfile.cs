using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Threads.Infrastracture.Migrations
{
    public partial class ExtendUsersProfile : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BannerObjectKey",
                table: "Users",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BannerObjectKey",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "Users");
        }
    }
}
