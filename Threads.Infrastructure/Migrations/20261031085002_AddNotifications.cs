using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Threads.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    PostId = table.Column<Guid>(type: "uuid", nullable: true),
                    CommentId = table.Column<Guid>(type: "uuid", nullable: true),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.CheckConstraint("CK_Notifications_DifferentUsers", "\"ActorId\" <> \"RecipientId\"");
                    table.CheckConstraint("CK_Notifications_Type", "\"Type\" IN ('Follow', 'PostLike', 'CommentLike', 'PostRepost', 'CommentRepost', 'PostComment', 'CommentReply', 'Quote')");
                    table.ForeignKey(
                        name: "FK_Notifications_Comments_CommentId",
                        column: x => x.CommentId,
                        principalTable: "Comments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Posts_PostId",
                        column: x => x.PostId,
                        principalTable: "Posts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_ActorId",
                        column: x => x.ActorId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Notifications_Users_RecipientId",
                        column: x => x.RecipientId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ActorId",
                table: "Notifications",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CommentId",
                table: "Notifications",
                column: "CommentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_PostId",
                table: "Notifications",
                column: "PostId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId",
                table: "Notifications",
                column: "RecipientId",
                filter: "\"ReadAt\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_RecipientId_CreatedAt_Id",
                table: "Notifications",
                columns: new[] { "RecipientId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_SourceId",
                table: "Notifications",
                column: "SourceId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Type_ActorId_SourceId",
                table: "Notifications",
                columns: new[] { "Type", "ActorId", "SourceId" },
                unique: true);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION sync_interaction_notification() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    event_data jsonb;
                    event_type text := TG_ARGV[0];
                    actor_id uuid;
                    recipient_id uuid;
                    source_id uuid;
                    post_id uuid;
                    comment_id uuid;
                    post_author_id uuid;
                    comment_author_id uuid;
                BEGIN
                    IF TG_OP = 'DELETE' THEN
                        event_data := to_jsonb(OLD);
                    ELSE
                        event_data := to_jsonb(NEW);
                    END IF;

                    IF TG_TABLE_NAME = 'Follows' THEN
                        actor_id := (event_data ->> 'FollowerId')::uuid;
                        recipient_id := (event_data ->> 'FollowingId')::uuid;
                        source_id := (event_data ->> 'Id')::uuid;
                    ELSIF TG_TABLE_NAME = 'Comments' THEN
                        actor_id := (event_data ->> 'AuthorId')::uuid;
                        source_id := (event_data ->> 'Id')::uuid;
                        post_id := (event_data ->> 'PostId')::uuid;
                        comment_id := source_id;
                        IF event_data ->> 'ParentCommentId' IS NOT NULL THEN
                            event_type := 'CommentReply';
                        END IF;
                    ELSIF TG_TABLE_NAME = 'PostQuotes' THEN
                        source_id := (event_data ->> 'PostId')::uuid;
                        IF event_data ->> 'TargetType' = 'Post' THEN
                            post_id := (event_data ->> 'TargetId')::uuid;
                        ELSE
                            comment_id := (event_data ->> 'TargetId')::uuid;
                        END IF;
                    ELSE
                        actor_id := (event_data ->> 'UserId')::uuid;
                        post_id := (event_data ->> 'PostId')::uuid;
                        comment_id := (event_data ->> 'CommentId')::uuid;
                        source_id := COALESCE(post_id, comment_id);
                    END IF;

                    IF TG_OP = 'DELETE' THEN
                        DELETE FROM "Notifications"
                        WHERE "Type" = event_type AND "SourceId" = source_id
                          AND (actor_id IS NULL OR "ActorId" = actor_id);
                        RETURN NULL;
                    END IF;

                    IF TG_TABLE_NAME = 'PostQuotes' THEN
                        SELECT source."AuthorId" INTO actor_id
                        FROM "Posts" source
                        WHERE source."Id" = source_id AND source."DeletedAt" IS NULL;
                        IF actor_id IS NULL THEN
                            RETURN NULL;
                        END IF;
                    END IF;

                    IF comment_id IS NOT NULL THEN
                        SELECT comment."AuthorId", comment."PostId"
                        INTO comment_author_id, post_id
                        FROM "Comments" comment
                        JOIN "Users" author ON author."Id" = comment."AuthorId"
                        WHERE comment."Id" = comment_id AND comment."DeletedAt" IS NULL
                          AND author."DeletedAt" IS NULL AND author."IsActive";
                        IF comment_author_id IS NULL THEN
                            RETURN NULL;
                        END IF;
                    END IF;

                    IF post_id IS NOT NULL THEN
                        SELECT post."AuthorId" INTO post_author_id
                        FROM "Posts" post
                        JOIN "Users" author ON author."Id" = post."AuthorId"
                        WHERE post."Id" = post_id AND post."DeletedAt" IS NULL
                          AND author."DeletedAt" IS NULL AND author."IsActive";
                        IF post_author_id IS NULL THEN
                            RETURN NULL;
                        END IF;
                    END IF;

                    IF TG_TABLE_NAME = 'Comments' THEN
                        IF event_type = 'CommentReply' THEN
                            SELECT parent."AuthorId" INTO recipient_id
                            FROM "Comments" parent
                            WHERE parent."Id" = (event_data ->> 'ParentCommentId')::uuid
                              AND parent."PostId" = post_id AND parent."DeletedAt" IS NULL;
                        ELSE
                            recipient_id := post_author_id;
                        END IF;
                    ELSE
                        recipient_id := COALESCE(recipient_id, comment_author_id, post_author_id);
                    END IF;

                    IF actor_id = recipient_id OR NOT EXISTS (
                        SELECT 1 FROM "Users" actor, "Users" recipient
                        WHERE actor."Id" = actor_id AND recipient."Id" = recipient_id
                          AND actor."DeletedAt" IS NULL AND actor."IsActive"
                          AND recipient."DeletedAt" IS NULL AND recipient."IsActive"
                    ) THEN
                        RETURN NULL;
                    END IF;

                    INSERT INTO "Notifications"
                        ("Id", "RecipientId", "ActorId", "Type", "SourceId", "PostId", "CommentId", "CreatedAt")
                    VALUES
                        (gen_random_uuid(), recipient_id, actor_id, event_type, source_id, post_id, comment_id, statement_timestamp())
                    ON CONFLICT ("Type", "ActorId", "SourceId") DO NOTHING;
                    RETURN NULL;
                END;
                $$;

                CREATE TRIGGER "TR_Follows_Notifications" AFTER INSERT OR DELETE ON "Follows"
                    FOR EACH ROW EXECUTE FUNCTION sync_interaction_notification('Follow');
                CREATE TRIGGER "TR_PostLikes_Notifications" AFTER INSERT OR DELETE ON "PostLikes"
                    FOR EACH ROW EXECUTE FUNCTION sync_interaction_notification('PostLike');
                CREATE TRIGGER "TR_CommentLikes_Notifications" AFTER INSERT OR DELETE ON "CommentLikes"
                    FOR EACH ROW EXECUTE FUNCTION sync_interaction_notification('CommentLike');
                CREATE TRIGGER "TR_PostReposts_Notifications" AFTER INSERT OR DELETE ON "PostReposts"
                    FOR EACH ROW EXECUTE FUNCTION sync_interaction_notification('PostRepost');
                CREATE TRIGGER "TR_CommentReposts_Notifications" AFTER INSERT OR DELETE ON "CommentReposts"
                    FOR EACH ROW EXECUTE FUNCTION sync_interaction_notification('CommentRepost');
                CREATE TRIGGER "TR_Comments_Notifications" AFTER INSERT OR DELETE ON "Comments"
                    FOR EACH ROW EXECUTE FUNCTION sync_interaction_notification('PostComment');
                CREATE TRIGGER "TR_PostQuotes_Notifications" AFTER INSERT OR DELETE ON "PostQuotes"
                    FOR EACH ROW EXECUTE FUNCTION sync_interaction_notification('Quote');

                CREATE FUNCTION cleanup_unavailable_notifications() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF TG_TABLE_NAME = 'Posts' AND NEW."DeletedAt" IS NOT NULL THEN
                        DELETE FROM "Notifications"
                        WHERE "PostId" = NEW."Id" OR ("Type" = 'Quote' AND "SourceId" = NEW."Id");
                    ELSIF TG_TABLE_NAME = 'Comments' AND NEW."DeletedAt" IS NOT NULL THEN
                        DELETE FROM "Notifications" WHERE "CommentId" = NEW."Id";
                    ELSIF TG_TABLE_NAME = 'Users' THEN
                        IF NEW."DeletedAt" IS NOT NULL OR NOT NEW."IsActive" THEN
                            DELETE FROM "Notifications"
                            WHERE "ActorId" = NEW."Id" OR "RecipientId" = NEW."Id"
                               OR "PostId" IN (SELECT "Id" FROM "Posts" WHERE "AuthorId" = NEW."Id")
                               OR "CommentId" IN (SELECT "Id" FROM "Comments" WHERE "AuthorId" = NEW."Id");
                        END IF;
                    END IF;
                    RETURN NULL;
                END;
                $$;

                CREATE TRIGGER "TR_Posts_CleanupNotifications" AFTER UPDATE OF "DeletedAt" ON "Posts"
                    FOR EACH ROW EXECUTE FUNCTION cleanup_unavailable_notifications();
                CREATE TRIGGER "TR_Comments_CleanupNotifications" AFTER UPDATE OF "DeletedAt" ON "Comments"
                    FOR EACH ROW EXECUTE FUNCTION cleanup_unavailable_notifications();
                CREATE TRIGGER "TR_Users_CleanupNotifications" AFTER UPDATE OF "DeletedAt", "IsActive" ON "Users"
                    FOR EACH ROW EXECUTE FUNCTION cleanup_unavailable_notifications();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                DROP TRIGGER "TR_Follows_Notifications" ON "Follows";
                DROP TRIGGER "TR_PostLikes_Notifications" ON "PostLikes";
                DROP TRIGGER "TR_CommentLikes_Notifications" ON "CommentLikes";
                DROP TRIGGER "TR_PostReposts_Notifications" ON "PostReposts";
                DROP TRIGGER "TR_CommentReposts_Notifications" ON "CommentReposts";
                DROP TRIGGER "TR_Comments_Notifications" ON "Comments";
                DROP TRIGGER "TR_PostQuotes_Notifications" ON "PostQuotes";
                DROP TRIGGER "TR_Posts_CleanupNotifications" ON "Posts";
                DROP TRIGGER "TR_Comments_CleanupNotifications" ON "Comments";
                DROP TRIGGER "TR_Users_CleanupNotifications" ON "Users";
                DROP FUNCTION sync_interaction_notification();
                DROP FUNCTION cleanup_unavailable_notifications();
                """);

            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
