using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class CommentLikeConfigurator : IEntityTypeConfiguration<CommentLike>
{
    public void Configure(EntityTypeBuilder<CommentLike> builder)
    {
        builder.ToTable("CommentLikes");

        builder.HasKey(commentLike => new { commentLike.CommentId, commentLike.UserId });

        builder.HasIndex(commentLike => new
        {
            commentLike.UserId,
            commentLike.CreatedAt,
            commentLike.CommentId
        });

        builder.Property(commentLike => commentLike.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(commentLike => commentLike.User)
            .WithMany(user => user.CommentLikes)
            .HasForeignKey(commentLike => commentLike.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(commentLike => commentLike.Comment)
            .WithMany(comment => comment.CommentLikes)
            .HasForeignKey(commentLike => commentLike.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
