using Microsoft.EntityFrameworkCore.Migrations;
using NpgsqlTypes;

#nullable disable

namespace Threads.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceRepositoryDataIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PollVotes_PollOptions_PollOptionId",
                table: "PollVotes");

            migrationBuilder.DropIndex(
                name: "IX_PollVotes_PollOptionId",
                table: "PollVotes");

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Users",
                type: "tsvector",
                nullable: true)
                .Annotation("Npgsql:TsVectorConfig", "simple")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Username", "DisplayName", "Location", "LocationCountry" });

            migrationBuilder.AddColumn<NpgsqlTsVector>(
                name: "SearchVector",
                table: "Posts",
                type: "tsvector",
                nullable: true)
                .Annotation("Npgsql:TsVectorConfig", "simple")
                .Annotation("Npgsql:TsVectorProperties", new[] { "Content", "LocationName", "EmbedTitle" });

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Media",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_PollOptions_PollId_Id",
                table: "PollOptions",
                columns: new[] { "PollId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SearchVector",
                table: "Users",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_Posts_SearchVector",
                table: "Posts",
                column: "SearchVector")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_PollVotes_PollId_PollOptionId",
                table: "PollVotes",
                columns: new[] { "PollId", "PollOptionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_PollVotes_PollOptions_PollId_PollOptionId",
                table: "PollVotes",
                columns: new[] { "PollId", "PollOptionId" },
                principalTable: "PollOptions",
                principalColumns: new[] { "PollId", "Id" },
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PollVotes_PollOptions_PollId_PollOptionId",
                table: "PollVotes");

            migrationBuilder.DropIndex(
                name: "IX_Users_SearchVector",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Posts_SearchVector",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_PollVotes_PollId_PollOptionId",
                table: "PollVotes");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_PollOptions_PollId_Id",
                table: "PollOptions");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "SearchVector",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Media");

            migrationBuilder.CreateIndex(
                name: "IX_PollVotes_PollOptionId",
                table: "PollVotes",
                column: "PollOptionId");

            migrationBuilder.AddForeignKey(
                name: "FK_PollVotes_PollOptions_PollOptionId",
                table: "PollVotes",
                column: "PollOptionId",
                principalTable: "PollOptions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}

