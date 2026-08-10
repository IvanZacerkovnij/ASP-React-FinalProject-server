using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Threads.Infrastracture.Migrations
{
    public partial class ExtendPosts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbedDescription",
                table: "Posts",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbedThumbnailUrl",
                table: "Posts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbedTitle",
                table: "Posts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbedUrl",
                table: "Posts",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLatitude",
                table: "Posts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "LocationLongitude",
                table: "Posts",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LocationName",
                table: "Posts",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RepostsCount",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ViewsCount",
                table: "Posts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Posts_RepostsCount",
                table: "Posts",
                sql: "\"RepostsCount\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Posts_ViewsCount",
                table: "Posts",
                sql: "\"ViewsCount\" >= 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Posts_RepostsCount",
                table: "Posts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Posts_ViewsCount",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "EmbedDescription",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "EmbedThumbnailUrl",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "EmbedTitle",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "EmbedUrl",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "LocationLatitude",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "LocationLongitude",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "LocationName",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "RepostsCount",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "ViewsCount",
                table: "Posts");
        }
    }
}
