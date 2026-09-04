using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class BookmarkConfigurator: IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("Bookmarks");
        
        builder.HasKey(bookmark => bookmark.Id);
        
        builder.Property(bookmark => bookmark.CreatedAt)
            .IsRequired();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Bookmarks_ExactlyOneTarget",
                "(\"PostId\" IS NOT NULL AND \"CommentId\" IS NULL) OR (\"PostId\" IS NULL AND \"CommentId\" IS NOT NULL)"));

        builder.HasIndex(bookmark => new {bookmark.UserId, bookmark.PostId})
            .IsUnique()
            .HasFilter("\"PostId\" IS NOT NULL");

        builder.HasIndex(bookmark => new { bookmark.UserId, bookmark.CommentId })
            .IsUnique()
            .HasFilter("\"CommentId\" IS NOT NULL");
        
        builder.HasOne(bookmark => bookmark.User)
            .WithMany(user => user.Bookmarks)
            .HasForeignKey(bookmark => bookmark.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        builder.HasOne(bookmark => bookmark.Post)
            .WithMany(post => post.Bookmarks)
            .HasForeignKey(bookmark => bookmark.PostId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bookmark => bookmark.Comment)
            .WithMany(comment => comment.Bookmarks)
            .HasForeignKey(bookmark => bookmark.CommentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
