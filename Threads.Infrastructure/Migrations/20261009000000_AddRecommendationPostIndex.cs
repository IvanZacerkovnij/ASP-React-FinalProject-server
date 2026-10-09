using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Threads.Infrastructure.Data;

namespace Threads.Infrastructure.Migrations;

[DbContext(typeof(ThreadsDbContext))]
[Migration("20261009000000_AddRecommendationPostIndex")]
public sealed partial class AddRecommendationPostIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Posts_CreatedAt_Id",
            table: "Posts",
            columns: new[] { "CreatedAt", "Id" },
            filter: "\"DeletedAt\" IS NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Posts_CreatedAt_Id", table: "Posts");
    }
}
