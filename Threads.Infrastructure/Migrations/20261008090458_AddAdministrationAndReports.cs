using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Threads.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrationAndReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Posts",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeletedAt",
                table: "Comments",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Decision = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    Reason = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    ReporterId = table.Column<Guid>(type: "uuid", nullable: true),
                    SystemCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    SystemLabel = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    ResolvedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_Users_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Users_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_DeletedAt",
                table: "Users",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsActive_CreatedAt_Id",
                table: "Users",
                columns: new[] { "IsActive", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Posts_DeletedAt",
                table: "Posts",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_DeletedAt",
                table: "Comments",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_CreatedAt",
                table: "Reports",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Decision",
                table: "Reports",
                column: "Decision");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId",
                table: "Reports",
                column: "ReporterId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ResolvedById",
                table: "Reports",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Source",
                table: "Reports",
                column: "Source");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status",
                table: "Reports",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_TargetType_TargetId",
                table: "Reports",
                columns: new[] { "TargetType", "TargetId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropIndex(
                name: "IX_Users_DeletedAt",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_IsActive_CreatedAt_Id",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Posts_DeletedAt",
                table: "Posts");

            migrationBuilder.DropIndex(
                name: "IX_Comments_DeletedAt",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Posts");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Comments");
        }
    }
}
