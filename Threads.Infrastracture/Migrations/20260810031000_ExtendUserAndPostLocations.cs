using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Threads.Infrastracture.Migrations
{
    public partial class ExtendUserAndPostLocations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LocationCountry",
                table: "Users",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLatitude",
                table: "Users",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLongitude",
                table: "Users",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationPlaceId",
                table: "Users",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationCountry",
                table: "Posts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationPlaceId",
                table: "Posts",
                type: "character varying(1024)",
                maxLength: 1024,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LocationCountry",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationPlaceId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LocationCountry",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "LocationPlaceId",
                table: "Posts");
        }
    }
}
