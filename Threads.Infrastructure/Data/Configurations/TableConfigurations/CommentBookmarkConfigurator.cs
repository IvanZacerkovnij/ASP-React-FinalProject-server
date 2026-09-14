using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class CommentBookmarkConfigurator : IEntityTypeConfiguration<CommentBookmark>
{
    public void Configure(EntityTypeBuilder<CommentBookmark> builder)
    {
        builder.ToTable("CommentBookmarks");

        builder.HasKey(commentBookmark => new { commentBookmark.CommentId, commentBookmark.UserId });

        builder.HasIndex(commentBookmark => new
        {
            commentBookmark.UserId,
            commentBookmark.CreatedAt,
            commentBookmark.CommentId
        });

        builder.Property(commentBookmark => commentBookmark.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(commentBookmark => commentBookmark.User)
            .WithMany(user => user.CommentBookmarks)
            .HasForeignKey(commentBookmark => commentBookmark.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(commentBookmark => commentBookmark.Comment)
            .WithMany(comment => comment.CommentBookmarks)
            .HasForeignKey(commentBookmark => commentBookmark.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
