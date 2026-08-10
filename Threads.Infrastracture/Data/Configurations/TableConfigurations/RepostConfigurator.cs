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

        builder.HasIndex(repost => new { repost.UserId, repost.PostId })
            .IsUnique();

        builder.HasOne(repost => repost.User)
            .WithMany(user => user.Reposts)
            .HasForeignKey(repost => repost.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(repost => repost.Post)
            .WithMany(post => post.Reposts)
            .HasForeignKey(repost => repost.PostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
