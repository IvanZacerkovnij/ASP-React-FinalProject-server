using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Threads.Infrastracture.Migrations
{
    public partial class AddMediaMetadata : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DurationSeconds",
                table: "Media",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Height",
                table: "Media",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailStorageKey",
                table: "Media",
                type: "character varying(512)",
                maxLength: 512,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Width",
                table: "Media",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Media_DurationSeconds",
                table: "Media",
                sql: "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Media_Height",
                table: "Media",
                sql: "\"Height\" IS NULL OR \"Height\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Media_Width",
                table: "Media",
                sql: "\"Width\" IS NULL OR \"Width\" >= 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Media_DurationSeconds",
                table: "Media");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Media_Height",
                table: "Media");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Media_Width",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "DurationSeconds",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Height",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "ThumbnailStorageKey",
                table: "Media");

            migrationBuilder.DropColumn(
                name: "Width",
                table: "Media");
        }
    }
