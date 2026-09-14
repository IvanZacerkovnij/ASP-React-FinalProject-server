using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class CommentRepostConfigurator : IEntityTypeConfiguration<CommentRepost>
{
    public void Configure(EntityTypeBuilder<CommentRepost> builder)
    {
        builder.ToTable("CommentReposts");

        builder.HasKey(commentRepost => new { commentRepost.CommentId, commentRepost.UserId });

        builder.HasIndex(commentRepost => new
        {
            commentRepost.UserId,
            commentRepost.CreatedAt,
            commentRepost.CommentId
        });

        builder.Property(commentRepost => commentRepost.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(commentRepost => commentRepost.User)
            .WithMany(user => user.CommentReposts)
            .HasForeignKey(commentRepost => commentRepost.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(commentRepost => commentRepost.Comment)
            .WithMany(comment => comment.CommentReposts)
            .HasForeignKey(commentRepost => commentRepost.CommentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
