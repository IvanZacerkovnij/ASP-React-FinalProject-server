using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class PostRepostConfigurator : IEntityTypeConfiguration<PostRepost>
{
    public void Configure(EntityTypeBuilder<PostRepost> builder)
    {
        builder.ToTable("PostReposts");

        builder.HasKey(postRepost => new { postRepost.PostId, postRepost.UserId });

        builder.HasIndex(postRepost => new
        {
            postRepost.UserId,
            postRepost.CreatedAt,
            postRepost.PostId
        });

        builder.Property(postRepost => postRepost.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(postRepost => postRepost.User)
            .WithMany(user => user.PostReposts)
            .HasForeignKey(postRepost => postRepost.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(postRepost => postRepost.Post)
            .WithMany(post => post.PostReposts)
            .HasForeignKey(postRepost => postRepost.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
