using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class LikeConfigurator : IEntityTypeConfiguration<Like>
{
    public void Configure(EntityTypeBuilder<Like> builder)
    {
        builder.ToTable("Likes");

        builder.HasKey(like => like.Id);

        builder.Property(like => like.CreatedAt)
            .IsRequired();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Likes_ExactlyOneTarget",
                "(\"PostId\" IS NOT NULL AND \"CommentId\" IS NULL) OR (\"PostId\" IS NULL AND \"CommentId\" IS NOT NULL)"));

        builder.HasIndex(like => new { like.UserId, like.PostId })
            .IsUnique()
            .HasFilter("\"PostId\" IS NOT NULL");

        builder.HasIndex(like => new { like.UserId, like.CommentId })
            .IsUnique()
            .HasFilter("\"CommentId\" IS NOT NULL");

        builder.HasOne(like => like.User)
            .WithMany(user => user.Likes)
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(like => like.Post)
            .WithMany(post => post.Likes)
            .HasForeignKey(like => like.PostId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(like => like.Comment)
            .WithMany(comment => comment.Likes)
            .HasForeignKey(like => like.CommentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
