using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class CommentConfigurator : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.ToTable("Comments");

        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.Content)
            .IsRequired()
            .HasMaxLength(1000);

        builder.Property(comment => comment.LinkPreviewUrl)
            .HasMaxLength(2048);

        builder.Property(comment => comment.LinkPreviewTitle)
            .HasMaxLength(255);

        builder.Property(comment => comment.LinkPreviewImageUrl)
            .HasMaxLength(2048);

        builder.Property(comment => comment.LocationName)
            .HasMaxLength(255);

        builder.Property(comment => comment.LocationPlaceId)
            .HasMaxLength(1024);

        builder.Property(comment => comment.LocationCountry)
            .HasMaxLength(255);

        builder.Property(comment => comment.CurrentVersionId)
            .IsRequired();

        builder.HasIndex(comment => comment.CurrentVersionId)
            .IsUnique();

        builder.Property(comment => comment.CreatedAt)
            .IsRequired();

        builder.HasIndex(comment => comment.DeletedAt);

        builder.HasIndex(comment => new { comment.PostId, comment.CreatedAt, comment.Id });
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

        builder.HasMany(comment => comment.Media)
            .WithOne(media => media.Comment)
            .HasForeignKey(media => media.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(comment => comment.Poll)
            .WithOne(poll => poll.Comment)
            .HasForeignKey<Poll>(poll => poll.CommentId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
