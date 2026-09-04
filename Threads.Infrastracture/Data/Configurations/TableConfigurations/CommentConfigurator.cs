using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class CommentConfigurator : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(comment => comment.CreatedAt)
            .IsRequired();

        builder.HasIndex(comment => comment.PostId);
        builder.HasIndex(comment => comment.AuthorId);
        builder.HasIndex(comment => comment.ParentCommentId);

        builder.HasOne(comment => comment.Post)
            .WithMany(post => post.Comments)
            .HasForeignKey(comment => comment.PostId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.Author)
            .WithMany(user => user.Comments)
            .HasForeignKey(comment => comment.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.ParentComment)
            .WithMany(comment => comment.Replies)
            .HasForeignKey(comment => comment.ParentCommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(comment => comment.Likes)
            .WithOne(like => like.Comment)
            .HasForeignKey(like => like.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(comment => comment.Bookmarks)
            .WithOne(bookmark => bookmark.Comment)
            .HasForeignKey(bookmark => bookmark.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(comment => comment.Reposts)
            .WithOne(repost => repost.Comment)
            .HasForeignKey(repost => repost.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(comment => comment.Views)
            .WithOne(view => view.Comment)
            .HasForeignKey(view => view.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
