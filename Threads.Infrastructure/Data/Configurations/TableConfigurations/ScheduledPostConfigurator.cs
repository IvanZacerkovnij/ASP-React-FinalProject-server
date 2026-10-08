using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Threads.Domain.Entities;

namespace Threads.Infrastructure.Data.Configurations.TableConfigurations;

public sealed class ScheduledPostConfigurator : IEntityTypeConfiguration<ScheduledPost>
{
    public void Configure(EntityTypeBuilder<ScheduledPost> builder)
    {
        builder.ToTable("ScheduledPosts");

        builder.HasKey(scheduledPost => scheduledPost.Id);

        builder.Property(scheduledPost => scheduledPost.Version)
            .IsRowVersion();

        builder.Property(scheduledPost => scheduledPost.Content)
            .HasMaxLength(2000);

        builder.Property(scheduledPost => scheduledPost.LinkPreviewUrl)
            .HasMaxLength(2048);

        builder.Property(scheduledPost => scheduledPost.LinkPreviewTitle)
            .HasMaxLength(255);

        builder.Property(scheduledPost => scheduledPost.LinkPreviewImageUrl)
            .HasMaxLength(2048);

        builder.Property(scheduledPost => scheduledPost.ScheduledAt)
            .IsRequired();

        builder.Property(scheduledPost => scheduledPost.CreatedAt)
            .IsRequired();

        builder.HasIndex(scheduledPost => new
        {
            scheduledPost.AuthorId,
            scheduledPost.ScheduledAt,
            scheduledPost.Id
        });

        builder.HasIndex(scheduledPost => new
        {
            scheduledPost.ScheduledAt,
            scheduledPost.Id
        });

        builder.HasOne(scheduledPost => scheduledPost.Author)
            .WithMany(user => user.ScheduledPosts)
            .HasForeignKey(scheduledPost => scheduledPost.AuthorId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(scheduledPost => scheduledPost.Media)
            .WithOne(media => media.ScheduledPost)
            .HasForeignKey(media => media.ScheduledPostId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
