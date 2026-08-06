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

        builder.HasIndex(like => new { like.UserId, like.PostId })
            .IsUnique();

        builder.HasOne(like => like.User)
            .WithMany(user => user.Likes)
            .HasForeignKey(like => like.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(like => like.Post)
            .WithMany(post => post.Likes)
            .HasForeignKey(like => like.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
