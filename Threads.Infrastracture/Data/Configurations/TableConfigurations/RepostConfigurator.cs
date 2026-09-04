using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastracture.Data.Configurations.TableConfigurations;

public class RepostConfigurator : IEntityTypeConfiguration<Repost>
{
    public void Configure(EntityTypeBuilder<Repost> builder)
    {
        builder.ToTable("Reposts");

        builder.HasKey(repost => repost.Id);

        builder.Property(repost => repost.CreatedAt)
            .IsRequired();

        builder.ToTable(table =>
            table.HasCheckConstraint(
                "CK_Reposts_ExactlyOneTarget",
                "(\"PostId\" IS NOT NULL AND \"CommentId\" IS NULL) OR (\"PostId\" IS NULL AND \"CommentId\" IS NOT NULL)"));

        builder.HasIndex(repost => new { repost.UserId, repost.PostId })
            .IsUnique()
            .HasFilter("\"PostId\" IS NOT NULL");

        builder.HasIndex(repost => new { repost.UserId, repost.CommentId })
            .IsUnique()
            .HasFilter("\"CommentId\" IS NOT NULL");

        builder.HasOne(repost => repost.User)
            .WithMany(user => user.Reposts)
            .HasForeignKey(repost => repost.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(repost => repost.Post)
            .WithMany(post => post.Reposts)
            .HasForeignKey(repost => repost.PostId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(repost => repost.Comment)
            .WithMany(comment => comment.Reposts)
            .HasForeignKey(repost => repost.CommentId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
