using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public class PostLikeConfigurator : IEntityTypeConfiguration<PostLike>
{
    public void Configure(EntityTypeBuilder<PostLike> builder)
    {
        builder.ToTable("PostLikes");

        builder.HasKey(postLike => new { postLike.PostId, postLike.UserId });

        builder.HasIndex(postLike => new
        {
            postLike.UserId,
            postLike.CreatedAt,
            postLike.PostId
        });

        builder.Property(postLike => postLike.CreatedAt)
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne(postLike => postLike.User)
            .WithMany(user => user.PostLikes)
            .HasForeignKey(postLike => postLike.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(postLike => postLike.Post)
            .WithMany(post => post.PostLikes)
            .HasForeignKey(postLike => postLike.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
